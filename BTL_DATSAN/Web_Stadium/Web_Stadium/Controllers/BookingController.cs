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
        private readonly EmailService _emailService;

        public BookingController(
            SanBongContext context,
            IRepository<DatSan> datSanRepo,
            IRepository<KhungGio> khungGioRepo,
            IConfiguration config,
            IHubContext<SanBongHub> hub,
            EmailService emailService)
        {
            _context = context;
            _datSanRepo = datSanRepo;
            _khungGioRepo = khungGioRepo;
            _config = config;
            _hub = hub;
            _emailService = emailService;
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/Create?khungGioId=1&ngay=2024-04-15
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> Create(int khungGioId, string? ngayStr)
        {
            var userId = TokenHelper.LayUserId(Request, _config);

            // Parse ngày an toàn — tránh SqlDateTime overflow
            if (!DateTime.TryParse(ngayStr, out var ngay) || ngay < new DateTime(1753, 1, 1))
                ngay = DateTime.Today;

            var khungGio = await _context.KhungGios
                .Include(k => k.SanBong)
                    .ThenInclude(s => s.DichVus)
                        .ThenInclude(d => d.DanhMucDichVu)
                .FirstOrDefaultAsync(k => k.Id == khungGioId);

            if (khungGio == null) return NotFound();

            // Kiểm tra hết hạn giữ chỗ
            if (khungGio.TrangThai == "DangGiu"
                && khungGio.ThoiGianHetGiuCho < DateTime.Now)
            {
                khungGio.TrangThai = "Trong";
                khungGio.ThoiGianHetGiuCho = null;
                await _khungGioRepo.UpdateAsync(khungGio);
                await _hub.Clients.Group($"san_{khungGio.SanBongId}")
                    .SendAsync("CapNhatKhungGio", new { khungGioId = khungGio.Id, trangThai = "Trong" });
            }

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
            var vouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId
                          && !uv.IsUsed
                          && uv.NgayHetHan > DateTime.Now)
                .OrderBy(uv => uv.NgayHetHan)
                .ToListAsync();

            ViewBag.KhungGio = khungGio;
            ViewBag.Ngay = ngay;
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
            int? voucherSanId,
            int? voucherHeThongId)
        {
            if (!DateTime.TryParse(ngayThiDauStr, out var ngayThiDau) || ngayThiDau < new DateTime(1753, 1, 1))
                ngayThiDau = DateTime.Today;

            if (ngayThiDau.Date < DateTime.Today)
            {
                TempData["Error"] = "Không thể đặt sân cho ngày đã qua. Vui lòng chọn ngày hôm nay hoặc tương lai!";
                return RedirectToAction("Details", "Venues", new { id = khungGioId });
            }

            var userId = TokenHelper.LayUserId(Request, _config);

            var userCheck = await _context.Users.FindAsync(userId);
            if (userCheck == null || !userCheck.DaXacThucSdt)
            {
                var returnUrl = $"/Booking/Create?khungGioId={khungGioId}&ngayThiDauStr={ngayThiDauStr}";
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

            if (ngayThiDau.Date == DateTime.Today)
            {
                var gioBD = khungGio.GioBatDau.ToTimeSpan();
                if (DateTime.Now.TimeOfDay >= gioBD)
                {
                    TempData["Error"] = "Khung giờ này đã qua hôm nay. Vui lòng chọn ngày khác!";
                    return RedirectToAction("Details", "Venues", new { id = khungGio.SanBongId });
                }
            }

            // ── Tính tiền dịch vụ ──────────────────────────────
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

            // voucher chỉ áp vào giá sân, dịch vụ tính riêng không giảm
            decimal giaSan = khungGio.Gia;
            decimal giaGoc = giaSan + tongTienDichVu;
            decimal tienGiamSan = 0;
            decimal tienGiamHeThong = 0;
            int? voucherSanApDungId = null;
            int? voucherHeThongApDungId = null;
            var now = DateTime.Now;

            if (voucherSanId.HasValue)
            {
                var v = await _context.Vouchers.FindAsync(voucherSanId.Value);
                if (v != null && v.IsActive && v.LoaiVoucher == "Owner"
                    && now >= v.NgayBatDau && now <= v.NgayHetHan
                    && (v.SoLuong == 0 || v.DaDung < v.SoLuong)
                    && giaSan >= v.DieuKienToiThieu)
                {
                    tienGiamSan = v.LoaiGiam == "PhanTram"
                        ? giaSan * v.GiaTriGiam / 100m
                        : Math.Min(v.GiaTriGiam, giaSan);
                    if (v.LoaiGiam == "PhanTram" && v.GiamToiDa.HasValue)
                        tienGiamSan = Math.Min(tienGiamSan, v.GiamToiDa.Value);
                    v.DaDung++;
                    voucherSanApDungId = v.Id;
                }
            }

            // voucher hệ thống áp vào giá sân sau khi đã giảm voucher sân
            decimal sanSauGiamSan = Math.Max(0, giaSan - tienGiamSan);
            if (voucherHeThongId.HasValue)
            {
                var v = await _context.Vouchers.FindAsync(voucherHeThongId.Value);
                if (v != null && v.IsActive && v.LoaiVoucher == "HeThong"
                    && now >= v.NgayBatDau && now <= v.NgayHetHan
                    && (v.SoLuong == 0 || v.DaDung < v.SoLuong)
                    && sanSauGiamSan >= v.DieuKienToiThieu)
                {
                    tienGiamHeThong = v.LoaiGiam == "PhanTram"
                        ? sanSauGiamSan * v.GiaTriGiam / 100m
                        : Math.Min(v.GiaTriGiam, sanSauGiamSan);
                    if (v.LoaiGiam == "PhanTram" && v.GiamToiDa.HasValue)
                        tienGiamHeThong = Math.Min(tienGiamHeThong, v.GiamToiDa.Value);
                    v.DaDung++;
                    voucherHeThongApDungId = v.Id;
                }
            }

            // tổng = sân sau giảm + dịch vụ (không giảm)
            decimal tongSauGiam = Math.Max(0, giaSan - tienGiamSan - tienGiamHeThong) + tongTienDichVu;
            var tyLeCoc = khungGio.SanBong?.TyLeCoc ?? 0.30m;
            decimal tienCoc = tongSauGiam * tyLeCoc;

            var maDatSan = $"XN-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

            var datSan = new DatSan
            {
                UserId = userId!.Value,
                KhungGioId = khungGioId,
                NgayThiDau = ngayThiDau,
                TienGoc = giaGoc,
                TienGiamSan = tienGiamSan,
                TienGiamHeThong = tienGiamHeThong,
                TienCoc = tienCoc,
                TongTien = tongSauGiam,
                VoucherSanId = voucherSanApDungId,
                VoucherHeThongId = voucherHeThongApDungId,
                MaXacNhan = maDatSan,
                TrangThai = "ChoDuyet",
                ThoiGianTao = DateTime.Now
            };
            await _datSanRepo.AddAsync(datSan);

            foreach (var (dvId, sl, gia) in dichVuList)
            {
                _context.DatSanDichVus.Add(new DatSanDichVu
                {
                    DatSanId = datSan.Id,
                    DichVuId = dvId,
                    SoLuong = sl
                });
            }
            if (dichVuList.Any() || voucherSanApDungId.HasValue || voucherHeThongApDungId.HasValue)
                await _context.SaveChangesAsync();

            // Khoá slot
            khungGio.TrangThai = "DaDat";
            khungGio.ThoiGianHetGiuCho = null;
            await _khungGioRepo.UpdateAsync(khungGio);
            await _hub.Clients.Group($"san_{khungGio.SanBongId}")
                .SendAsync("CapNhatKhungGio", new { khungGioId = khungGio.Id, trangThai = "DaDat" });

            var tongGiam = tienGiamSan + tienGiamHeThong;
            var msgGiam = tongGiam > 0 ? $" (Đã giảm {tongGiam:N0}đ từ voucher)" : "";
            TempData["Success"] = $"Đặt sân thành công! Mã: {maDatSan}. Tiền cọc: {tienCoc:N0}đ{msgGiam}. Vui lòng chờ Owner xác nhận.";
            return RedirectToAction("MyBookings");
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/LayVoucher?sanBongId=1&tongTien=200000
        // ══════════════════════════════════════════════════════════
        [HttpGet]
        [YeuCauDangNhap]
        public async Task<IActionResult> LayVoucher(int sanBongId, decimal tongTien)
        {
            var now = DateTime.Now;

            var rawSan = await _context.Vouchers
                .Where(v => v.LoaiVoucher == "Owner"
                         && v.SanBongId == sanBongId
                         && v.IsActive
                         && v.NgayBatDau <= now
                         && v.NgayHetHan >= now
                         && (v.SoLuong == 0 || v.DaDung < v.SoLuong)
                         && tongTien >= v.DieuKienToiThieu)
                .ToListAsync();

            var rawHT = await _context.Vouchers
                .Where(v => v.LoaiVoucher == "HeThong"
                         && v.IsActive
                         && v.NgayBatDau <= now
                         && v.NgayHetHan >= now
                         && (v.SoLuong == 0 || v.DaDung < v.SoLuong)
                         && tongTien >= v.DieuKienToiThieu)
                .ToListAsync();

            decimal TinhGiam(Voucher v, decimal gia)
            {
                var g = v.LoaiGiam == "PhanTram" ? gia * v.GiaTriGiam / 100m : Math.Min(v.GiaTriGiam, gia);
                if (v.LoaiGiam == "PhanTram" && v.GiamToiDa.HasValue) g = Math.Min(g, v.GiamToiDa.Value);
                return g;
            }

            var voucherSan = rawSan
                .Select(v => new
                {
                    v.Id, v.TenVoucher, v.MoTa, v.LoaiGiam,
                    v.GiaTriGiam, v.GiamToiDa, v.NgayHetHan,
                    v.DieuKienToiThieu,
                    conLai = v.SoLuong == 0 ? -1 : v.SoLuong - v.DaDung,
                    soTienGiam = TinhGiam(v, tongTien),
                    conNgay = (int)(v.NgayHetHan - now).TotalDays
                })
                .OrderByDescending(x => x.soTienGiam)
                .ToList();

            var voucherHeThong = rawHT
                .Select(v => new
                {
                    v.Id, v.TenVoucher, v.MoTa, v.LoaiGiam,
                    v.GiaTriGiam, v.GiamToiDa, v.NgayHetHan,
                    v.DieuKienToiThieu,
                    conLai = v.SoLuong == 0 ? -1 : v.SoLuong - v.DaDung,
                    soTienGiam = TinhGiam(v, tongTien),
                    conNgay = (int)(v.NgayHetHan - now).TotalDays
                })
                .OrderByDescending(x => x.soTienGiam)
                .ToList();

            return Json(new { voucherSan, voucherHeThong });
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/Details/{id}
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> Details(int id)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var don = await _context.DatSans
                .AsNoTracking()
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong).ThenInclude(s => s.Owner)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Include(d => d.VoucherSan)
                .Include(d => d.VoucherHeThong)
                .Include(d => d.StaffCheckIn)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (don == null) return NotFound();
            return View(don);
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/MyBookings
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> MyBookings(string? trangThai)
        {
            var userId = TokenHelper.LayUserId(Request, _config);

            var query = _context.DatSans
                .AsNoTracking()
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Include(d => d.VoucherSan)
                .Include(d => d.VoucherHeThong)
                .Where(d => d.UserId == userId);

            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(d => d.TrangThai == trangThai);

            var list = await query
                .OrderByDescending(d => d.ThoiGianTao)
                .Take(100)
                .ToListAsync();

            // Đơn nào đã có đánh giá
            var daDanhGiaIds = await _context.DanhGias
                .AsNoTracking()
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
            // ❌ FIX 4: Bắt buộc nhập lý do hủy
            if (string.IsNullOrWhiteSpace(lyDoHuy))
            {
                TempData["Error"] = "Vui lòng nhập lý do hủy đặt sân!";
                return RedirectToAction("MyBookings");
            }

            var userId = TokenHelper.LayUserId(Request, _config);
            var datSan = await _context.DatSans
                .Include(d => d.KhungGio)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (datSan == null) return NotFound();

            if (datSan.TrangThai == "DangSuDung" || datSan.TrangThai == "HoanThanh")
            {
                TempData["Error"] = "Không thể huỷ đơn đang diễn ra hoặc đã hoàn thành!";
                return RedirectToAction("MyBookings");
            }

            // ── Tính tiền hoàn theo đúng chính sách ─────────────
            decimal phanTramHoan = 1.0m; // mặc định 100%
            string thongBaoHoan;

            if (datSan.TrangThai == "DaXacNhan")
            {
                // Đã được Owner xác nhận → tính theo thời gian còn lại
                var gioBatDau = datSan.KhungGio?.GioBatDau.ToTimeSpan() ?? TimeSpan.Zero;
                var gioDauTran = datSan.NgayThiDau.Date.Add(gioBatDau);
                var conLai = gioDauTran - DateTime.Now;

                if (conLai.TotalHours >= 24)
                {
                    phanTramHoan = 1.0m;   // trước 24h: hoàn 100%
                    thongBaoHoan = "Hoàn 100% tiền cọc vì huỷ trước 24 giờ.";
                }
                else if (conLai.TotalHours >= 2)
                {
                    phanTramHoan = 0.5m;   // trong 24h: hoàn 50%
                    thongBaoHoan = "Hoàn 50% tiền cọc vì huỷ trong vòng 24 giờ.";
                }
                else
                {
                    phanTramHoan = 0m;     // trong 2h: không hoàn
                    thongBaoHoan = "Không hoàn cọc vì huỷ trong vòng 2 giờ trước trận.";
                }
            }
            else
            {
                // ChoDuyet → hoàn 100% (Owner chưa cam kết gì)
                thongBaoHoan = "Hoàn 100% tiền cọc vì Owner chưa xác nhận.";
            }

            var soTienHoan = Math.Round(datSan.TienCoc * phanTramHoan, 0);
            datSan.TrangThai = "DaHuy";
            datSan.GhiChuSuCo = $"Lý do hủy: {lyDoHuy}";  // Lưu lý do hủy
            datSan.KhungGio.TrangThai = "Trong";

            await _context.SaveChangesAsync();
            await _hub.Clients.Group($"san_{datSan.KhungGio.SanBongId}")
                .SendAsync("CapNhatKhungGio", new { khungGioId = datSan.KhungGioId, trangThai = "Trong" });

            TempData["Success"] = $"Đã huỷ đặt sân. {thongBaoHoan} Hoàn {soTienHoan:N0}đ trong 1–3 ngày làm việc.";
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

        // ══════════════════════════════════════════════════════════
        // GET /Booking/KhungGioTrong — AJAX endpoint
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> KhungGioTrong(int sanId, string ngay, int khungGioHienTaiId)
        {
            if (!DateTime.TryParse(ngay, out var ngayParse)) ngayParse = DateTime.Today;
            var list = await _context.KhungGios
                .Where(k => k.SanBongId == sanId && k.TrangThai == "Trong" && k.Id != khungGioHienTaiId)
                .OrderBy(k => k.GioBatDau)
                .Select(k => new {
                    id = k.Id,
                    gioBatDau = k.GioBatDau.ToString(),
                    gioKetThuc = k.GioKetThuc.ToString(),
                    gia = k.Gia.ToString("N0")
                })
                .ToListAsync();
            return Json(list);
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/YeuCauDoiGio/{datSanId}
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> YeuCauDoiGio(int datSanId)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);

            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            {
                TempData["Error"] = "Chỉ có thể yêu cầu đổi giờ với đơn đã xác nhận.";
                return RedirectToAction("MyBookings");
            }

            var daCo = await _context.YeuCauDoiGios
                .AnyAsync(y => y.DatSanId == datSanId && y.TrangThai == "ChoXuLy");
            if (daCo)
            {
                TempData["Error"] = "Đơn này đã có yêu cầu đổi giờ đang chờ xử lý.";
                return RedirectToAction("MyBookings");
            }

            var sanId = don.KhungGio.SanBongId;
            var khungGioTrong = await _context.KhungGios
                .Where(k => k.SanBongId == sanId && k.TrangThai == "Trong" && k.Id != don.KhungGioId)
                .OrderBy(k => k.GioBatDau)
                .ToListAsync();

            ViewBag.DatSan = don;
            ViewBag.KhungGioList = khungGioTrong;
            ViewBag.SanBong = don.KhungGio.SanBong;
            return View();
        }

        // ══════════════════════════════════════════════════════════
        // POST /Booking/GuiYeuCauDoiGio
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> GuiYeuCauDoiGio(
            int datSanId, int khungGioMoiId, string ngayMoiStr, string lyDo)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);

            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            {
                TempData["Error"] = "Chỉ có thể yêu cầu đổi giờ với đơn đã xác nhận.";
                return RedirectToAction("MyBookings");
            }

            if (string.IsNullOrWhiteSpace(lyDo) || lyDo.Trim().Length < 20)
            {
                TempData["Error"] = "Lý do đổi giờ phải ít nhất 20 ký tự.";
                return RedirectToAction("YeuCauDoiGio", new { datSanId });
            }

            if (khungGioMoiId == don.KhungGioId)
            {
                TempData["Error"] = "Khung giờ mới phải khác khung giờ hiện tại.";
                return RedirectToAction("YeuCauDoiGio", new { datSanId });
            }

            if (!DateTime.TryParse(ngayMoiStr, out var ngayMoi))
                ngayMoi = don.NgayThiDau;

            var daCo = await _context.YeuCauDoiGios
                .AnyAsync(y => y.DatSanId == datSanId && y.TrangThai == "ChoXuLy");
            if (daCo)
            {
                TempData["Error"] = "Đơn này đã có yêu cầu đổi giờ đang chờ xử lý.";
                return RedirectToAction("MyBookings");
            }

            var yeuCau = new YeuCauDoiGio
            {
                DatSanId = datSanId,
                UserId = userId!.Value,
                KhungGioMoiId = khungGioMoiId,
                NgayMoi = ngayMoi,
                LyDo = lyDo.Trim(),
                TrangThai = "ChoXuLy",
                ThoiGianTao = DateTime.Now
            };
            _context.YeuCauDoiGios.Add(yeuCau);
            await _context.SaveChangesAsync();

            // Gửi email cho Staff của sân này
            var sanId = don.KhungGio.SanBongId;
            var staffList = await _context.StaffSanPhanCongs
                .Include(s => s.Staff)
                .Where(s => s.SanBongId == sanId)
                .ToListAsync();
            var kg = await _context.KhungGios.FindAsync(khungGioMoiId);
            var gioMoi = kg != null
                ? $"{kg.GioBatDau:hh\\:mm} – {kg.GioKetThuc:hh\\:mm}"
                : "N/A";
            foreach (var sp in staffList)
            {
                if (!string.IsNullOrEmpty(sp.Staff?.Email))
                    _ = Task.Run(() => _emailService.GuiEmailYeuCauMoiChoStaff(
                        sp.Staff.Email, sp.Staff.HoTen ?? "Staff",
                        don.User?.HoTen ?? "Khách", don.KhungGio.SanBong?.TenSan ?? "",
                        gioMoi, ngayMoi.ToString("dd/MM/yyyy"), lyDo.Trim()));
            }

            TempData["Success"] = "Đã gửi yêu cầu đổi khung giờ! Staff sẽ xem xét và phản hồi sớm.";
            return RedirectToAction("MyBookings");
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/YeuCauDoiSan/{datSanId}
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> YeuCauDoiSan(int datSanId)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);

            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            {
                TempData["Error"] = "Chỉ có thể yêu cầu đổi sân với đơn đã xác nhận.";
                return RedirectToAction("MyBookings");
            }

            var daCo = await _context.YeuCauDoiSans
                .AnyAsync(y => y.DatSanId == datSanId && y.TrangThai == "ChoXuLy");
            if (daCo)
            {
                TempData["Error"] = "Đơn này đã có yêu cầu đổi sân đang chờ xử lý.";
                return RedirectToAction("MyBookings");
            }

            var loaiSan = don.KhungGio?.SanBong?.LoaiSan;
            var sanList = await _context.SanBongs
                .Where(s => s.TrangThaiDuyet == "DaDuyet"
                         && !s.IsHidden
                         && s.Id != don.KhungGio!.SanBongId
                         && (loaiSan == null || s.LoaiSan == loaiSan))
                .OrderBy(s => s.TenSan)
                .ToListAsync();

            ViewBag.DatSan = don;
            ViewBag.SanList = sanList;
            ViewBag.NgayThiDau = don.NgayThiDau.ToString("yyyy-MM-dd");
            return View();
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/KhungGioTrongCuaSan — AJAX endpoint
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> KhungGioTrongCuaSan(int sanId, string? ngay)
        {
            var list = await _context.KhungGios
                .Where(k => k.SanBongId == sanId && k.TrangThai == "Trong")
                .OrderBy(k => k.GioBatDau)
                .Select(k => new {
                    id = k.Id,
                    gioBatDau = k.GioBatDau.ToString(),
                    gioKetThuc = k.GioKetThuc.ToString(),
                    gia = (double)k.Gia,
                    giaFmt = k.Gia.ToString("N0")
                })
                .ToListAsync();
            return Json(list);
        }

        // ══════════════════════════════════════════════════════════
        // POST /Booking/GuiYeuCauDoiSan
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> GuiYeuCauDoiSan(
            int datSanId, int sanMoiId, int khungGioMoiId, string lyDo)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);

            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            {
                TempData["Error"] = "Chỉ có thể yêu cầu đổi sân với đơn đã xác nhận.";
                return RedirectToAction("MyBookings");
            }

            if (string.IsNullOrWhiteSpace(lyDo) || lyDo.Trim().Length < 10)
            {
                TempData["Error"] = "Lý do đổi sân phải ít nhất 10 ký tự.";
                return RedirectToAction("YeuCauDoiSan", new { datSanId });
            }

            var kgMoi = await _context.KhungGios
                .Include(k => k.SanBong)
                .FirstOrDefaultAsync(k => k.Id == khungGioMoiId && k.SanBongId == sanMoiId);

            if (kgMoi == null || kgMoi.TrangThai != "Trong")
            {
                TempData["Error"] = "Khung giờ đã chọn không còn trống. Vui lòng chọn lại.";
                return RedirectToAction("YeuCauDoiSan", new { datSanId });
            }

            var daCo = await _context.YeuCauDoiSans
                .AnyAsync(y => y.DatSanId == datSanId && y.TrangThai == "ChoXuLy");
            if (daCo)
            {
                TempData["Error"] = "Đơn này đã có yêu cầu đổi sân đang chờ xử lý.";
                return RedirectToAction("MyBookings");
            }

            var chenhLech = kgMoi.Gia - (don.KhungGio?.Gia ?? 0);

            var yeuCau = new YeuCauDoiSan
            {
                DatSanId = datSanId,
                UserId = userId!.Value,
                SanMoiId = sanMoiId,
                KhungGioMoiId = khungGioMoiId,
                NgayThiDau = don.NgayThiDau,
                LyDo = lyDo.Trim(),
                TrangThai = "ChoXuLy",
                ChenhLechGia = chenhLech,
                ThoiGianTao = DateTime.Now
            };
            _context.YeuCauDoiSans.Add(yeuCau);
            await _context.SaveChangesAsync();

            // Gửi email Owner sân mới
            var sanMoi = kgMoi.SanBong;
            if (sanMoi != null)
            {
                var owner = await _context.Users.FindAsync(sanMoi.OwnerId);
                if (owner != null && !string.IsNullOrEmpty(owner.Email))
                {
                    var gioMoi = $"{kgMoi.GioBatDau:hh\\:mm} – {kgMoi.GioKetThuc:hh\\:mm}";
                    _ = Task.Run(() => _emailService.GuiEmailYeuCauDoiSanChoOwner(
                        owner.Email, owner.HoTen ?? "Owner",
                        don.User?.HoTen ?? "Khách",
                        don.KhungGio?.SanBong?.TenSan ?? "",
                        sanMoi.TenSan, gioMoi,
                        don.NgayThiDau.ToString("dd/MM/yyyy"),
                        lyDo.Trim(), chenhLech));
                }
            }

            TempData["Success"] = "Đã gửi yêu cầu đổi sân! Owner sân mới sẽ xem xét và phản hồi sớm.";
            return RedirectToAction("MyBookings");
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/GhepTran/{datSanId} — form gửi lời mời
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> GhepTran(int datSanId, int? datSanBId)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);

            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            {
                TempData["Error"] = "Chỉ có thể ghép trận với đơn đã xác nhận.";
                return RedirectToAction("MyBookings");
            }

            var daCo = await _context.GhepTrans
                .AnyAsync(g => (g.DatSanAId == datSanId || g.DatSanBId == datSanId)
                            && (g.TrangThai == "ChoXacNhan" || g.TrangThai == "DaXacNhan"));
            if (daCo)
            {
                TempData["Error"] = "Đơn này đã có yêu cầu ghép trận đang xử lý.";
                return RedirectToAction("MyBookings");
            }

            ViewBag.DatSan = don;

            if (datSanBId.HasValue)
            {
                var presel = await _context.DatSans
                    .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Id == datSanBId.Value && d.TrangThai == "DaXacNhan");
                ViewBag.PreselDatSanB = presel;
            }

            return View();
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/TimTuMatchmaking — AJAX tìm bài Matchmaking cùng ngày/giờ
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> TimTuMatchmaking(int datSanAId)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var donA = await _context.DatSans
                .Include(d => d.KhungGio)
                .FirstOrDefaultAsync(d => d.Id == datSanAId && d.UserId == userId);

            if (donA == null || donA.KhungGio == null) return Json(new object[0]);

            var list = await _context.Matchmakings
                .Include(m => m.DatSan)
                    .ThenInclude(d => d.KhungGio)
                        .ThenInclude(k => k.SanBong)
                .Include(m => m.User)
                .Where(m => m.TrangThai == "DangTim"
                         && m.UserId != userId
                         && m.DatSan.NgayThiDau.Date == donA.NgayThiDau.Date
                         && m.DatSan.KhungGio.GioBatDau == donA.KhungGio.GioBatDau)
                .ToListAsync();

            return Json(list.Select(m => new {
                datSanId = m.DatSanId,
                tenUser = m.User?.HoTen ?? "Ẩn danh",
                tieuDe = m.TieuDe,
                tenSan = m.DatSan?.KhungGio?.SanBong?.TenSan ?? "",
                diaChi = m.DatSan?.KhungGio?.SanBong?.DiaChi ?? "",
                gio = $"{m.DatSan?.KhungGio?.GioBatDau:HH\\:mm} – {m.DatSan?.KhungGio?.GioKetThuc:HH\\:mm}"
            }));
        }

        // ══════════════════════════════════════════════════════════
        // POST /Booking/TimDoiThu — AJAX tìm đơn cùng ngày/giờ
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> TimDoiThu(int datSanAId)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var donA = await _context.DatSans
                .Include(d => d.KhungGio)
                .FirstOrDefaultAsync(d => d.Id == datSanAId && d.UserId == userId);

            if (donA == null) return Json(new List<object>());

            var gioBatDau = donA.KhungGio.GioBatDau;
            var ngay = donA.NgayThiDau.Date;

            var danhSach = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => d.UserId != userId
                         && d.TrangThai == "DaXacNhan"
                         && d.NgayThiDau.Date == ngay
                         && d.KhungGio.GioBatDau == gioBatDau)
                .ToListAsync();

            // Lọc đơn chưa bị ghép với đơn của mình
            var daGhep = await _context.GhepTrans
                .Where(g => g.TrangThai == "ChoXacNhan" || g.TrangThai == "DaXacNhan")
                .Select(g => new { g.DatSanAId, g.DatSanBId })
                .ToListAsync();

            var ketQua = danhSach
                .Where(d => !daGhep.Any(g =>
                    (g.DatSanAId == d.Id || g.DatSanBId == d.Id) ||
                    (g.DatSanAId == datSanAId || g.DatSanBId == datSanAId)))
                .Select(d => new {
                    datSanId = d.Id,
                    tenUser = d.User?.HoTen ?? "Ẩn danh",
                    tenSan = d.KhungGio?.SanBong?.TenSan ?? "",
                    diaChi = d.KhungGio?.SanBong?.DiaChi ?? "",
                    gio = d.KhungGio?.GioBatDau.ToString("HH:mm") + " – " + d.KhungGio?.GioKetThuc.ToString("HH:mm")
                })
                .ToList();

            return Json(ketQua);
        }

        // ══════════════════════════════════════════════════════════
        // POST /Booking/GuiLoiMoiGhep
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> GuiLoiMoiGhep(
            int datSanAId, int datSanBId, string loiNhan)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var donA = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == datSanAId && d.UserId == userId);
            var donB = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanBId && d.TrangThai == "DaXacNhan");

            if (donA == null || donB == null)
            { TempData["Error"] = "Đơn không hợp lệ."; return RedirectToAction("MyBookings"); }
            if (donA.TrangThai != "DaXacNhan")
            { TempData["Error"] = "Đơn của bạn phải ở trạng thái Đã xác nhận."; return RedirectToAction("MyBookings"); }
            if (donB.UserId == userId)
            { TempData["Error"] = "Không thể gửi lời mời cho chính mình."; return RedirectToAction("MyBookings"); }

            var daCo = await _context.GhepTrans
                .AnyAsync(g => (g.DatSanAId == datSanAId || g.DatSanBId == datSanAId)
                            && (g.TrangThai == "ChoXacNhan" || g.TrangThai == "DaXacNhan"));
            if (daCo)
            { TempData["Error"] = "Đơn này đã có yêu cầu ghép trận đang xử lý."; return RedirectToAction("MyBookings"); }

            var ghep = new GhepTran
            {
                DatSanAId = datSanAId,
                DatSanBId = datSanBId,
                UserAId = userId!.Value,
                UserBId = donB.UserId,
                LoiNhan = loiNhan?.Trim() ?? "",
                TrangThai = "ChoXacNhan",
                ThoiGianTao = DateTime.Now
            };
            _context.GhepTrans.Add(ghep);
            await _context.SaveChangesAsync();

            // Email User B
            if (donB.User != null && !string.IsNullOrEmpty(donB.User.Email))
            {
                var tenSanA = donA.KhungGio?.SanBong?.TenSan ?? "";
                var ngay = donA.NgayThiDau.ToString("dd/MM/yyyy");
                var gio = $"{donA.KhungGio?.GioBatDau:HH\\:mm} – {donA.KhungGio?.GioKetThuc:HH\\:mm}";
                _ = Task.Run(() => _emailService.GuiEmailLoiMoiGhepTran(
                    donB.User.Email, donB.User.HoTen ?? "Khách",
                    donA.User?.HoTen ?? "Đội A",
                    tenSanA, ngay, gio, loiNhan?.Trim() ?? ""));
            }

            TempData["Success"] = "Đã gửi lời mời ghép trận! Chờ đội kia xác nhận.";
            return RedirectToAction("MyBookings");
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/LoiMoiGhepTran — danh sách lời mời cho User B
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> LoiMoiGhepTran()
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var list = await _context.GhepTrans
                .Include(g => g.DatSanA)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(g => g.DatSanA).ThenInclude(d => d.User)
                .Include(g => g.DatSanB)
                    .ThenInclude(d => d!.KhungGio).ThenInclude(k => k!.SanBong)
                .Include(g => g.UserA)
                .Where(g => g.UserBId == userId && g.TrangThai == "ChoXacNhan")
                .OrderByDescending(g => g.ThoiGianTao)
                .ToListAsync();
            return View(list);
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/DemLoiMoiGhep — AJAX badge count
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> DemLoiMoiGhep()
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var count = await _context.GhepTrans
                .CountAsync(g => g.UserBId == userId && g.TrangThai == "ChoXacNhan");
            return Json(new { count });
        }

        // ══════════════════════════════════════════════════════════
        // POST /Booking/XacNhanGhepTran
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> XacNhanGhepTran(int ghepTranId, string hanhDong)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var ghep = await _context.GhepTrans
                .Include(g => g.DatSanA).ThenInclude(d => d.User)
                .Include(g => g.UserA)
                .FirstOrDefaultAsync(g => g.Id == ghepTranId
                    && g.UserBId == userId
                    && g.TrangThai == "ChoXacNhan");

            if (ghep == null) return NotFound();

            if (hanhDong == "TuChoi")
            {
                ghep.TrangThai = "TuChoi";
                ghep.ThoiGianXuLy = DateTime.Now;
                await _context.SaveChangesAsync();

                var userA = ghep.DatSanA.User;
                var userBten = (await _context.Users.FindAsync(userId))?.HoTen ?? "Đội B";
                if (userA != null && !string.IsNullOrEmpty(userA.Email))
                    _ = Task.Run(() => _emailService.GuiEmailGhepTranTuChoi(
                        userA.Email, userA.HoTen ?? "Đội A", userBten));

                TempData["Success"] = "Đã từ chối lời mời ghép trận.";
                return RedirectToAction("LoiMoiGhepTran");
            }

            // ChapNhan → chuyển sang trang chọn sân
            ghep.TrangThai = "DaXacNhan";
            ghep.ThoiGianXuLy = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction("ChonSanGhep", new { ghepTranId });
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/ChonSanGhep/{ghepTranId}
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> ChonSanGhep(int ghepTranId)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var ghep = await _context.GhepTrans
                .Include(g => g.DatSanA)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(g => g.DatSanA).ThenInclude(d => d.User)
                .Include(g => g.DatSanB)
                    .ThenInclude(d => d!.KhungGio).ThenInclude(k => k!.SanBong)
                .Include(g => g.DatSanB).ThenInclude(d => d!.User)
                .Include(g => g.UserA)
                .FirstOrDefaultAsync(g => g.Id == ghepTranId
                    && (g.UserAId == userId || g.UserBId == userId)
                    && g.TrangThai == "DaXacNhan");

            if (ghep == null) return NotFound();

            var sanList = await _context.SanBongs
                .Where(s => s.TrangThaiDuyet == "DaDuyet" && !s.IsHidden)
                .OrderBy(s => s.TenSan)
                .ToListAsync();

            ViewBag.GhepTran = ghep;
            ViewBag.SanList = sanList;
            return View();
        }

        // ══════════════════════════════════════════════════════════
        // POST /Booking/ChonSanGhep
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> ChonSanGhep(
            int ghepTranId, string hinhThuc, int? sanMoiId, int? khungGioMoiId)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var ghep = await _context.GhepTrans
                .Include(g => g.DatSanA)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(g => g.DatSanA).ThenInclude(d => d.User)
                .Include(g => g.DatSanB)
                    .ThenInclude(d => d!.KhungGio).ThenInclude(k => k!.SanBong)
                .Include(g => g.DatSanB).ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(g => g.Id == ghepTranId
                    && (g.UserAId == userId || g.UserBId == userId)
                    && g.TrangThai == "DaXacNhan");

            if (ghep == null) return NotFound();

            var donA = ghep.DatSanA;
            var donB = ghep.DatSanB;
            var kgA = donA.KhungGio;
            var kgB = donB?.KhungGio;

            ghep.HinhThuc = hinhThuc;
            ghep.TrangThai = "HoanTat";
            ghep.ThoiGianXuLy = DateTime.Now;

            string tenSan, ngay, gio, maXacNhan;

            if (hinhThuc == "SanA")
            {
                // Giữ đơn A, hủy đơn B
                if (donB != null)
                {
                    donB.TrangThai = "DaHuy";
                    if (kgB != null) { kgB.TrangThai = "Trong"; kgB.ThoiGianHetGiuCho = null; }
                }
                ghep.SanChonId = kgA.SanBongId;
                ghep.KhungGioChonId = donA.KhungGioId;
                tenSan = kgA.SanBong?.TenSan ?? "";
                ngay = donA.NgayThiDau.ToString("dd/MM/yyyy");
                gio = $"{kgA.GioBatDau:HH\\:mm} – {kgA.GioKetThuc:HH\\:mm}";
                maXacNhan = donA.MaXacNhan;
                await _context.SaveChangesAsync();
                if (kgB != null)
                    await _hub.Clients.Group($"san_{kgB.SanBongId}")
                        .SendAsync("CapNhatKhungGio", new { khungGioId = kgB.Id, trangThai = "Trong", hetHan = (DateTime?)null });

                _ = Task.Run(() => _emailService.GuiEmailGhepTranHoanTat(
                    donA.User?.Email ?? "", donA.User?.HoTen ?? "Đội A",
                    donB?.User?.Email ?? "", donB?.User?.HoTen ?? "Đội B",
                    tenSan, ngay, gio, maXacNhan));
            }
            else if (hinhThuc == "SanB")
            {
                // Giữ đơn B, hủy đơn A
                donA.TrangThai = "DaHuy";
                if (kgA != null) { kgA.TrangThai = "Trong"; kgA.ThoiGianHetGiuCho = null; }
                if (donB != null)
                {
                    ghep.SanChonId = donB.KhungGio?.SanBongId;
                    ghep.KhungGioChonId = donB.KhungGioId;
                }
                tenSan = donB?.KhungGio?.SanBong?.TenSan ?? "";
                ngay = (donB?.NgayThiDau ?? donA.NgayThiDau).ToString("dd/MM/yyyy");
                gio = $"{donB?.KhungGio?.GioBatDau:HH\\:mm} – {donB?.KhungGio?.GioKetThuc:HH\\:mm}";
                maXacNhan = donB?.MaXacNhan ?? "";
                await _context.SaveChangesAsync();
                await _hub.Clients.Group($"san_{kgA.SanBongId}")
                    .SendAsync("CapNhatKhungGio", new { khungGioId = kgA.Id, trangThai = "Trong", hetHan = (DateTime?)null });

                _ = Task.Run(() => _emailService.GuiEmailGhepTranHoanTat(
                    donA.User?.Email ?? "", donA.User?.HoTen ?? "Đội A",
                    donB?.User?.Email ?? "", donB?.User?.HoTen ?? "Đội B",
                    tenSan, ngay, gio, maXacNhan));
            }
            else // SanMoi
            {
                if (sanMoiId == null || khungGioMoiId == null)
                {
                    TempData["Error"] = "Vui lòng chọn sân mới và khung giờ.";
                    return RedirectToAction("ChonSanGhep", new { ghepTranId });
                }

                var kgMoi = await _context.KhungGios
                    .Include(k => k.SanBong)
                    .FirstOrDefaultAsync(k => k.Id == khungGioMoiId && k.SanBongId == sanMoiId);

                if (kgMoi == null || kgMoi.TrangThai != "Trong")
                {
                    TempData["Error"] = "Khung giờ đã chọn không còn trống.";
                    return RedirectToAction("ChonSanGhep", new { ghepTranId });
                }

                // Hủy cả 2 đơn
                donA.TrangThai = "DaHuy";
                if (kgA != null) { kgA.TrangThai = "Trong"; kgA.ThoiGianHetGiuCho = null; }
                if (donB != null)
                {
                    donB.TrangThai = "DaHuy";
                    if (kgB != null) { kgB.TrangThai = "Trong"; kgB.ThoiGianHetGiuCho = null; }
                }

                // Tạo đơn mới
                var tienCocMoi = Math.Round(kgMoi.Gia * (kgMoi.SanBong?.TyLeCoc ?? 0.30m), 0);
                var donMoi = new DatSan
                {
                    UserId = ghep.UserAId,
                    KhungGioId = khungGioMoiId.Value,
                    NgayThiDau = donA.NgayThiDau,
                    TienCoc = tienCocMoi,
                    TongTien = 0,
                    MaXacNhan = $"GT{DateTime.Now:yyMMddHHmm}{ghep.Id:D4}",
                    TrangThai = "DaXacNhan",
                    ThoiGianTao = DateTime.Now
                };
                _context.DatSans.Add(donMoi);
                kgMoi.TrangThai = "DaDat";

                ghep.SanChonId = sanMoiId;
                ghep.KhungGioChonId = khungGioMoiId;
                await _context.SaveChangesAsync();

                // SignalR broadcast tất cả sân liên quan
                if (kgA != null)
                    await _hub.Clients.Group($"san_{kgA.SanBongId}")
                        .SendAsync("CapNhatKhungGio", new { khungGioId = kgA.Id, trangThai = "Trong", hetHan = (DateTime?)null });
                if (kgB != null)
                    await _hub.Clients.Group($"san_{kgB.SanBongId}")
                        .SendAsync("CapNhatKhungGio", new { khungGioId = kgB.Id, trangThai = "Trong", hetHan = (DateTime?)null });
                await _hub.Clients.Group($"san_{sanMoiId}")
                    .SendAsync("CapNhatKhungGio", new { khungGioId = khungGioMoiId, trangThai = "DaDat", hetHan = (DateTime?)null });

                tenSan = kgMoi.SanBong?.TenSan ?? "";
                ngay = donA.NgayThiDau.ToString("dd/MM/yyyy");
                gio = $"{kgMoi.GioBatDau:HH\\:mm} – {kgMoi.GioKetThuc:HH\\:mm}";
                maXacNhan = donMoi.MaXacNhan;
                _ = Task.Run(() => _emailService.GuiEmailGhepTranSanMoi(
                    donA.User?.Email ?? "", donA.User?.HoTen ?? "Đội A",
                    donB?.User?.Email ?? "", donB?.User?.HoTen ?? "Đội B",
                    tenSan, ngay, gio, maXacNhan));
            }

            // Tự động đóng bài Matchmaking liên quan
            var dsIds = new List<int> { ghep.DatSanAId };
            if (ghep.DatSanBId.HasValue) dsIds.Add(ghep.DatSanBId.Value);
            var mmToClose = await _context.Matchmakings
                .Where(m => dsIds.Contains(m.DatSanId) && m.TrangThai == "DangTim")
                .ToListAsync();
            if (mmToClose.Any())
            {
                foreach (var m in mmToClose) m.TrangThai = "DaDong";
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Ghép trận hoàn tất! Cả hai đội sẽ nhận được email xác nhận.";
            return RedirectToAction("MyBookings");
        }

        // ══════════════════════════════════════════════════════════
        // GET /Booking/LayThongBao — AJAX notification feed
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap]
        public async Task<IActionResult> LayThongBao()
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            if (userId == null) return Json(Array.Empty<object>());

            var now = DateTime.Now;
            var nguong48h = now.AddHours(-48);

            static string TinhThoiGian(DateTime t)
            {
                var d = DateTime.Now - t;
                if (d.TotalMinutes < 1)  return "Vừa xong";
                if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} phút trước";
                if (d.TotalHours < 24)   return $"{(int)d.TotalHours} giờ trước";
                return $"{(int)d.TotalDays} ngày trước";
            }

            var unread = new List<object>();
            var read   = new List<object>();

            // ── 1. Lời mời ghép trận (UserB = mình, chờ xác nhận) ──
            var gheps = await _context.GhepTrans
                .Include(g => g.UserA)
                .Include(g => g.DatSanA)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(g => g.UserBId == userId && g.TrangThai == "ChoXacNhan")
                .OrderByDescending(g => g.ThoiGianTao)
                .Take(5)
                .ToListAsync();

            foreach (var g in gheps)
                unread.Add(new {
                    loai = "GhepTran",
                    tieuDe = "Lời mời ghép trận mới!",
                    moTa = $"{g.UserA?.HoTen ?? "Ai đó"} muốn ghép trận tại {g.DatSanA?.KhungGio?.SanBong?.TenSan ?? "sân bóng"}",
                    thoiGian = TinhThoiGian(g.ThoiGianTao),
                    url = "/Booking/LoiMoiGhepTran",
                    daDoc = false
                });

            // ── 2. Yêu cầu đổi giờ đã xử lý (trong 48h) ──
            var doiGios = await _context.YeuCauDoiGios
                .Include(y => y.DatSan)
                .Include(y => y.KhungGioMoi)
                .Where(y => y.UserId == userId
                         && (y.TrangThai == "DaPheDuyet" || y.TrangThai == "TuChoi")
                         && y.ThoiGianXuLy >= nguong48h)
                .OrderByDescending(y => y.ThoiGianXuLy)
                .Take(5)
                .ToListAsync();

            foreach (var y in doiGios)
            {
                var ok  = y.TrangThai == "DaPheDuyet";
                var gio = y.KhungGioMoi != null
                    ? $"{y.KhungGioMoi.GioBatDau:HH\\:mm}–{y.KhungGioMoi.GioKetThuc:HH\\:mm}"
                    : "";
                var daDoc = y.ThoiGianXuLy < now.AddHours(-2);
                var item = new {
                    loai = "DoiGio",
                    tieuDe = ok ? "Yêu cầu đổi giờ được phê duyệt!" : "Yêu cầu đổi giờ bị từ chối",
                    moTa = ok
                        ? $"Đơn {y.DatSan?.MaXacNhan} đã đổi sang {gio}"
                        : $"Đơn {y.DatSan?.MaXacNhan}: yêu cầu đổi giờ không được chấp thuận",
                    thoiGian = TinhThoiGian(y.ThoiGianXuLy ?? y.ThoiGianTao),
                    url = "/Booking/MyBookings",
                    daDoc
                };
                if (daDoc) read.Add(item); else unread.Add(item);
            }

            // ── 3. Yêu cầu đổi sân đã xử lý (trong 48h) ──
            var doiSans = await _context.YeuCauDoiSans
                .Include(y => y.DatSan)
                .Include(y => y.SanMoi)
                .Where(y => y.UserId == userId
                         && (y.TrangThai == "DaPheDuyet" || y.TrangThai == "TuChoi")
                         && y.ThoiGianXuLy >= nguong48h)
                .OrderByDescending(y => y.ThoiGianXuLy)
                .Take(5)
                .ToListAsync();

            foreach (var y in doiSans)
            {
                var ok    = y.TrangThai == "DaPheDuyet";
                var daDoc = y.ThoiGianXuLy < now.AddHours(-2);
                var item = new {
                    loai = "DoiSan",
                    tieuDe = ok ? "Yêu cầu đổi sân được phê duyệt!" : "Yêu cầu đổi sân bị từ chối",
                    moTa = ok
                        ? $"Đơn {y.DatSan?.MaXacNhan} đã chuyển sang {y.SanMoi?.TenSan}"
                        : $"Đơn {y.DatSan?.MaXacNhan}: yêu cầu đổi sang {y.SanMoi?.TenSan} không được chấp thuận",
                    thoiGian = TinhThoiGian(y.ThoiGianXuLy ?? y.ThoiGianTao),
                    url = "/Booking/MyBookings",
                    daDoc
                };
                if (daDoc) read.Add(item); else unread.Add(item);
            }

            // ── 4. Chuyển nhượng — UserA có người tiếp nhận (ChoXacNhan) ──
            var cnChoXacNhan = await _context.ChuyenNhuongs
                .Include(c => c.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(c => c.UserB)
                .Where(c => c.UserAId == userId && c.TrangThai == "ChoXacNhan")
                .OrderByDescending(c => c.ThoiGianTao)
                .Take(5)
                .ToListAsync();

            foreach (var c in cnChoXacNhan)
                unread.Add(new {
                    loai = "ChuyenNhuong",
                    tieuDe = "Có người muốn tiếp nhận đơn của bạn!",
                    moTa = $"{c.UserB?.HoTen ?? "Ai đó"} muốn nhận đơn sân {c.DatSan?.KhungGio?.SanBong?.TenSan ?? ""}",
                    thoiGian = TinhThoiGian(c.ThoiGianTao),
                    url = "/ChuyenNhuong/XacNhan/" + c.Id,
                    daDoc = false
                });

            // ── 5. Chuyển nhượng hoàn tất — UserA được thông báo ──
            var cnHoanTat = await _context.ChuyenNhuongs
                .Include(c => c.DatSan)
                .Where(c => c.UserAId == userId
                         && c.TrangThai == "HoanTat"
                         && c.ThoiGianXuLy >= nguong48h)
                .OrderByDescending(c => c.ThoiGianXuLy)
                .Take(3)
                .ToListAsync();

            foreach (var c in cnHoanTat)
            {
                var daDoc = c.ThoiGianXuLy < now.AddHours(-2);
                var item = new {
                    loai = "ChuyenNhuong",
                    tieuDe = "Chuyển nhượng đơn hoàn tất!",
                    moTa = $"Đơn {c.DatSan?.MaXacNhan} đã được chuyển nhượng thành công.",
                    thoiGian = TinhThoiGian(c.ThoiGianXuLy ?? c.ThoiGianTao),
                    url = "/Booking/MyBookings",
                    daDoc
                };
                if (daDoc) read.Add(item); else unread.Add(item);
            }

            return Json(unread.Concat(read));
        }
    }
}