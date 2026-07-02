using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Hubs;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    public class BookingController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IRepository<DatSan> _datSanRepo;
        private readonly IRepository<KhungGio> _khungGioRepo;
        private readonly IConfiguration _config;
        private readonly IHubContext<SanBongHub> _hub;
        private readonly HoanCocService _hoanCocService;

        public BookingController(
            SanBongContext context,
            IRepository<DatSan> datSanRepo,
            IRepository<KhungGio> khungGioRepo,
            IConfiguration config,
            IHubContext<SanBongHub> hub,
            HoanCocService hoanCocService)
        {
            _context = context;
            _datSanRepo = datSanRepo;
            _khungGioRepo = khungGioRepo;
            _config = config;
            _hub = hub;
            _hoanCocService = hoanCocService;
        }
//
        // ══════════════════════════════════════════════════════════
        // GET /Booking/Create?khungGioId=1&ngay=2024-04-15
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> Create(int khungGioId, string? ngayStr)
        {
            // Parse ngày an toàn — tránh SqlDateTime overflow
            if (!DateTime.TryParse(ngayStr, out var ngay) || ngay < new DateTime(1753, 1, 1))
                ngay = DateTime.Today;

            // Xác định ngày hợp lệ: nếu nhỏ hơn hôm nay thì lấy ngày mai
            var ngayValid = ngay.Date;
            if (ngayValid < DateTime.Now.Date)
                ngayValid = DateTime.Now.Date.AddDays(1);

            // Lấy khung giờ kèm sân và dịch vụ
            var khungGio = await _context.KhungGios
                .Include(k => k.SanBong)
                    .ThenInclude(s => s.DichVus)
                        .ThenInclude(d => d.DanhMucDichVu)
                .FirstOrDefaultAsync(k => k.Id == khungGioId);

            if (khungGio == null) return NotFound();

            // Kiểm tra và giải phóng giữ chỗ hết hạn
            if (khungGio.TrangThai == "DangGiu" && khungGio.ThoiGianHetGiuCho < DateTime.Now)
            {
                khungGio.TrangThai = "Trong";
                khungGio.ThoiGianHetGiuCho = null;
                await _khungGioRepo.UpdateAsync(khungGio);
                await _hub.Clients.Group($"san_{khungGio.SanBongId}")
                    .SendAsync("CapNhatKhungGio", new { khungGioId = khungGio.Id, trangThai = "Trong" });
            }

            // Nếu khung giờ đã bị đặt
            if (khungGio.TrangThai == "DaDat")
            {
                TempData["Error"] = "Khung giờ này đã bị đặt!";
                return RedirectToAction("Details", "Venues", new { id = khungGio.SanBongId });
            }

            // Giữ chỗ 5 phút (theo flow)
            khungGio.TrangThai = "DangGiu";
            khungGio.ThoiGianHetGiuCho = DateTime.Now.AddMinutes(5);
            await _khungGioRepo.UpdateAsync(khungGio);
            await _hub.Clients.Group($"san_{khungGio.SanBongId}")
                .SendAsync("CapNhatKhungGio", new
                {
                    khungGioId = khungGio.Id,
                    trangThai = "DangGiu",
                    hetHan = khungGio.ThoiGianHetGiuCho
                });

            var tyLeCoc = khungGio.SanBong?.TyLeCoc ?? 0.30m;
            var dichVus = khungGio.SanBong?.DichVus
                .Where(d => d.IsActive && d.TonKho > 0)
                .ToList() ?? new();

            // Voucher user đang có (chưa dùng, chưa hết hạn)
            var userId = TokenHelper.LayUserId(Request, _config);
            var vouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId
                          && !uv.IsUsed
                          && uv.NgayHetHan > DateTime.Now)
                .OrderBy(uv => uv.NgayHetHan)
                .ToListAsync();

            ViewBag.KhungGio = khungGio;
            ViewBag.Ngay = ngayValid;
            ViewBag.TyLeCoc = tyLeCoc;
            ViewBag.TienCoc = khungGio.Gia * tyLeCoc;
            ViewBag.DichVus = dichVus;
            ViewBag.Vouchers = vouchers;
            ViewBag.HetHan = khungGio.ThoiGianHetGiuCho;

            return View();
        }


        // ══════════════════════════════════════════════════════════
        // POST /Booking/Create
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> Create(
            int khungGioId,
            string? ngayThiDauStr,
            List<int>? dichVuIds,
            List<int>? soLuongs,
            string? userVoucherId,
            string? maVoucherCongKhai)
        {
            // Parse ngày an toàn — tránh SqlDateTime overflow
            if (!DateTime.TryParse(ngayThiDauStr, out var ngayThiDau) || ngayThiDau < new DateTime(1753, 1, 1))
                ngayThiDau = DateTime.Today;

            // ❌ FIX 1: Không cho đặt ngày trong quá khứ
            if (ngayThiDau.Date < DateTime.Today)
            {
                TempData["Error"] = "Không thể đặt sân cho ngày đã qua. Vui lòng chọn ngày hôm nay hoặc tương lai!";
                return RedirectToAction("Details", "Venues", new { id = khungGioId });
            }

            var userId = TokenHelper.LayUserId(Request, _config);

            // ❌ FIX 2: Kiểm tra đã xác thực SĐT/Email chưa
            var userCheck = await _context.Users.FindAsync(userId);
            if (userCheck == null || !userCheck.DaXacThucSdt)
            {
                var returnUrl = $"/Booking/Create?khungGioId={khungGioId}&ngayThiDauStr={ngayThiDauStr}";
                // Giải phóng slot đang giữ
                var kgTam = await _context.KhungGios.FindAsync(khungGioId);
                if (kgTam != null && kgTam.TrangThai == "DangGiu")
                {
                    kgTam.TrangThai = "Trong";
                    kgTam.ThoiGianHetGiuCho = null;
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction("XacThuc", "Otp", new { returnUrl });
            }

            var khungGio = await _context.KhungGios
                .Include(k => k.SanBong)
                .FirstOrDefaultAsync(k => k.Id == khungGioId);
            if (khungGio == null) return NotFound();

            // ❌ FIX 1b: Nếu đặt hôm nay → kiểm tra khung giờ chưa qua
            if (ngayThiDau.Date == DateTime.Today)
            {
                var gioBD = khungGio.GioBatDau.ToTimeSpan();
                if (DateTime.Now.TimeOfDay >= gioBD)
                {
                    TempData["Error"] = "Khung giờ này đã qua hôm nay. Vui lòng chọn ngày khác!";
                    return RedirectToAction("Details", "Venues", new { id = khungGio.SanBongId });
                }
            }

            var tyLeCoc = khungGio.SanBong?.TyLeCoc ?? 0.30m;
            var tienCocGoc = khungGio.Gia * tyLeCoc;
            var tienCocSauGiam = tienCocGoc;

            // ── Áp dụng voucher nếu có ──────────────────────────
            UserVoucher? uvDung = null;
            Voucher? voucherCongKhai = null;

            if (!string.IsNullOrEmpty(userVoucherId))
            {
                uvDung = await _context.UserVouchers
                    .Include(uv => uv.Voucher)
                    .FirstOrDefaultAsync(uv => uv.MaSuDung == userVoucherId
                                            && uv.UserId == userId
                                            && !uv.IsUsed
                                            && uv.NgayHetHan > DateTime.Now);

                if (uvDung?.Voucher != null)
                {
                    var v = uvDung.Voucher;
                    if (v.LoaiGiam == "PhanTram")
                    {
                        var giam = tienCocGoc * (v.GiaTriGiam / 100m);
                        if (v.GiamToiDa.HasValue) giam = Math.Min(giam, v.GiamToiDa.Value);
                        tienCocSauGiam = Math.Max(0, tienCocGoc - giam);
                    }
                    else // SoTien
                    {
                        tienCocSauGiam = Math.Max(0, tienCocGoc - v.GiaTriGiam);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(maVoucherCongKhai))
            {
                var ma = maVoucherCongKhai.Trim().ToUpper();
                voucherCongKhai = await _context.Vouchers.FirstOrDefaultAsync(v =>
                    v.MaVoucher == ma
                    && v.IsActive
                    && v.LoaiPhatHanh == "CongKhai"
                    && (v.SanBongId == null || v.SanBongId == khungGio.SanBongId)
                    && (v.SoLuotConLai == null || v.SoLuotConLai > 0));

                if (voucherCongKhai == null)
                {
                    TempData["Error"] = $"Mã voucher \"{maVoucherCongKhai}\" không hợp lệ hoặc đã hết lượt.";
                    // Giải phóng slot đang giữ
                    if (khungGio.TrangThai == "DangGiu")
                    {
                        khungGio.TrangThai = "Trong";
                        khungGio.ThoiGianHetGiuCho = null;
                        await _context.SaveChangesAsync();
                    }
                    return RedirectToAction("Details", "Venues", new { id = khungGio.SanBongId });
                }

                if (voucherCongKhai.LoaiGiam == "PhanTram")
                {
                    var giam = tienCocGoc * (voucherCongKhai.GiaTriGiam / 100m);
                    if (voucherCongKhai.GiamToiDa.HasValue) giam = Math.Min(giam, voucherCongKhai.GiamToiDa.Value);
                    tienCocSauGiam = Math.Max(0, tienCocGoc - giam);
                }
                else
                {
                    tienCocSauGiam = Math.Max(0, tienCocGoc - voucherCongKhai.GiaTriGiam);
                }
            }

            // Sinh mã xác nhận theo format XN-YYYYMMDD-XXXX
            var maDatSan = $"XN-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

            // ✅ FIX 3: Tính tổng tiền dịch vụ để cộng vào TongTien
            decimal tongTienDichVu = 0;
            var dichVuList = new List<(int dvId, int sl, decimal gia)>();
            if (dichVuIds != null)
            {
                for (int i = 0; i < dichVuIds.Count; i++)
                {
                    var sl = (soLuongs != null && i < soLuongs.Count) ? soLuongs[i] : 1;
                    if (sl <= 0) continue;
                    var dv = await _context.DichVus.FindAsync(dichVuIds[i]);
                    if (dv == null || !dv.IsActive) continue;
                    tongTienDichVu += dv.Gia * sl;
                    dichVuList.Add((dichVuIds[i], sl, dv.Gia));
                }
            }

            // TongTien = tiền sân + dịch vụ (cọc tính % trên tiền sân)
            var tongTienSan = khungGio.Gia + tongTienDichVu;

            // Tính lại cọc nếu cần (cọc tính trên tiền sân, không tính dịch vụ)
            // tienCocSauGiam đã tính đúng rồi

            var datSan = new DatSan
            {
                UserId = userId!.Value,
                KhungGioId = khungGioId,
                NgayThiDau = ngayThiDau,
                TienCoc = tienCocSauGiam,
                TongTien = tongTienSan,   // ← FIX: bao gồm cả dịch vụ
                MaXacNhan = maDatSan,
                TrangThai = "ChoDuyet",
                ThoiGianTao = DateTime.Now
            };
            await _datSanRepo.AddAsync(datSan);

            // Thêm dịch vụ vào đơn (KHÔNG trừ kho — chỉ trừ khi Staff check-in)
            foreach (var (dvId, sl, gia) in dichVuList)
            {
                _context.DatSanDichVus.Add(new DatSanDichVu
                {
                    DatSanId = datSan.Id,
                    DichVuId = dvId,
                    SoLuong = sl
                });
            }
            if (dichVuList.Any()) await _context.SaveChangesAsync();

            // ── Đánh dấu voucher đã dùng ────────────────────────
            if (uvDung != null)
            {
                uvDung.IsUsed = true;
                uvDung.NgaySuDung = DateTime.Now;
                uvDung.DatSanId = datSan.Id;
                await _context.SaveChangesAsync();

                // Ghi log điểm (trừ điểm đã ghi khi đổi, ở đây chỉ ghi lại note dùng voucher)
            }
            else if (voucherCongKhai != null)
            {
                if (voucherCongKhai.SoLuotConLai.HasValue)
                    voucherCongKhai.SoLuotConLai = Math.Max(0, voucherCongKhai.SoLuotConLai.Value - 1);

                _context.UserVouchers.Add(new UserVoucher
                {
                    UserId = userId!.Value,
                    VoucherId = voucherCongKhai.Id,
                    MaSuDung = $"PUB-{Guid.NewGuid().ToString("N")[..10].ToUpper()}",
                    NgayDoi = DateTime.Now,
                    NgayHetHan = DateTime.Now.AddDays(voucherCongKhai.SoNgayHieuLuc > 0 ? voucherCongKhai.SoNgayHieuLuc : 30),
                    IsUsed = true,
                    NgaySuDung = DateTime.Now,
                    DatSanId = datSan.Id
                });
                await _context.SaveChangesAsync();
            }

            // Cập nhật trạng thái khung giờ thành "Đã đặt"
            khungGio.TrangThai = "DaDat";
            khungGio.ThoiGianHetGiuCho = null;
            await _khungGioRepo.UpdateAsync(khungGio);
            await _hub.Clients.Group($"san_{khungGio.SanBongId}")
                .SendAsync("CapNhatKhungGio", new { khungGioId = khungGio.Id, trangThai = "DaDat" });

            TempData["Success"] = $"Đặt sân thành công! Mã xác nhận: {maDatSan}. Vui lòng chờ Owner xác nhận.";
            return RedirectToAction("MyBookings");
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/MyBookings
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> MyBookings(string? trangThai)
        {
            var userId = TokenHelper.LayUserId(Request, _config);

            var query = _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Where(d => d.UserId == userId);

            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(d => d.TrangThai == trangThai);

            var list = await query
                .OrderByDescending(d => d.ThoiGianTao)
                .ToListAsync();

            // Đơn nào đã có đánh giá
            var daDanhGiaIds = await _context.DanhGias
                .Where(dg => dg.UserId == userId)
                .Select(dg => dg.DatSanId)
                .ToListAsync();
            ViewBag.DaDanhGiaIds = daDanhGiaIds.ToHashSet();

            // Tin matchmaking đang tìm
            var datSanIds = list.Select(d => d.Id).ToList();
            var daTim = await _context.Matchmakings
                .Where(m => m.TrangThai == "DangTim" && datSanIds.Contains(m.DatSanId))
                .ToListAsync();
            ViewBag.DaTim = daTim.Select(m => m.DatSanId).ToHashSet();
            ViewBag.MmIdMap = daTim.ToDictionary(m => m.DatSanId, m => m.Id);

            ViewBag.TrangThai = trangThai;
            return View(list);
        }

        // ══════════════════════════════════════════════════════════
        // POST /Booking/Huy
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> Huy(int id, string? lyDoHuy)
        {
            if (string.IsNullOrWhiteSpace(lyDoHuy))
            {
                TempData["Error"] = "Vui lòng nhập lý do hủy đặt sân!";
                return RedirectToAction("MyBookings");
            }

            var userId = TokenHelper.LayUserId(Request, _config);
            var datSan = await _context.DatSans
                .Include(d => d.KhungGio)
                    .ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (datSan == null) return NotFound();

            if (datSan.TrangThai == "DangSuDung" || datSan.TrangThai == "HoanThanh")
            {
                TempData["Error"] = "Không thể huỷ đơn đang diễn ra hoặc đã hoàn thành!";
                return RedirectToAction("MyBookings");
            }

            var (success, message) = await _hoanCocService.ThucHienHoanCocAsync(
                datSan,
                nguonHuy: "KhachTuHuy",
                vaiTroNguoiKhoiTao: "User",
                nguoiKhoiTaoId: userId,
                ghiChu: $"Lý do hủy: {lyDoHuy}"
            );

            if (!success)
            {
                TempData["Error"] = message;
                return RedirectToAction("MyBookings");
            }

            datSan.TrangThai = "DaHuy";
            datSan.GhiChuSuCo = $"Lý do hủy: {lyDoHuy}";
            datSan.KhungGio.TrangThai = "Trong";

            await _context.SaveChangesAsync();
            await _hub.Clients.Group($"san_{datSan.KhungGio.SanBongId}")
                .SendAsync("CapNhatKhungGio", new { khungGioId = datSan.KhungGioId, trangThai = "Trong" });

            TempData["Success"] = $"Đã huỷ đặt sân. {message} Hoàn trong 1–3 ngày làm việc.";
            return RedirectToAction("MyBookings");
        }

        // ══════════════════════════════════════════════════════════
        // POST /Booking/GuiKhieuNai
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> GuiKhieuNai(int datSanId, string lyDo)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var don = await _context.DatSans
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);
            if (don == null) return NotFound();

            var daCoKN = await _context.KhieuNais
                .AnyAsync(k => k.DatSanId == datSanId && k.UserId == userId!.Value);
            if (daCoKN)
            {
                TempData["Error"] = "Bạn đã gửi khiếu nại cho đơn này rồi!";
                return RedirectToAction("MyBookings");
            }

            _context.KhieuNais.Add(new KhieuNai
            {
                DatSanId = datSanId,
                UserId = userId!.Value,
                LyDo = lyDo,
                TrangThai = "ChoXuLy",
                NgayGui = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi khiếu nại! Admin sẽ xem xét và phản hồi sớm nhất.";
            return RedirectToAction("MyBookings");
        }
    }
}