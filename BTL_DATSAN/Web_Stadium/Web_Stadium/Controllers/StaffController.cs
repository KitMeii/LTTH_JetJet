using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Staff")]
    public class StaffController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        private readonly RecommendationApiService _recommendationService;

        public StaffController(SanBongContext context, IConfiguration config, EmailService emailService,
            RecommendationApiService recommendationService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
            _recommendationService = recommendationService;
        }

        private int GetStaffId() => TokenHelper.LayUserId(Request, _config)!.Value;

        // Helper lấy JWT thô (forward sang java-recommendation)
        private string GetJwt() => _recommendationService.GetJwtFromContext(HttpContext) ?? "";

        // CheckInService.java (java-recommendation) so sánh ownerId truyền vào với
        // SanBongs.OwnerId — Staff không có "ownerId" riêng nên phải tra ngược qua
        // Users.OwnerIdCuaStaff (chủ quản lý Staff này) rồi mới gọi sang Java.
        private async Task<int?> GetOwnerIdCuaStaffAsync()
        {
            var staffId = GetStaffId();
            return await _context.Users
                .Where(u => u.Id == staffId)
                .Select(u => u.OwnerIdCuaStaff)
                .FirstOrDefaultAsync();
        }

        // Lấy danh sách SanBongId mà Staff này được phân công
        private async Task<List<int>> GetSanDuocGiaoAsync()
        {
            var staffId = GetStaffId();
            return await _context.StaffSanPhanCongs
                .Where(p => p.StaffId == staffId)
                .Select(p => p.SanBongId)
                .ToListAsync();
        }

        // ══════════════════════════════════════════════════════════
        // 1. DASHBOARD CA TRỰC — Lịch đặt sân hôm nay
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var now = DateTime.Now;

            // Đơn hôm nay của sân được phân công — sắp xếp theo giờ
            var donHomNay = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.NgayThiDau.Date == now.Date
                         && d.TrangThai != "DaHuy")
                .OrderBy(d => d.KhungGio.GioBatDau)
                .ToListAsync();

            // Đơn DangSuDung — đang diễn ra
            ViewBag.DangDien = donHomNay.Where(d => d.TrangThai == "DangSuDung").ToList();
            // Đơn DaXacNhan — sắp đến
            ViewBag.SapDen = donHomNay.Where(d => d.TrangThai == "DaXacNhan").ToList();
            // Đơn HoanThanh — đã xong hôm nay
            ViewBag.HoanThanh = donHomNay.Where(d => d.TrangThai == "HoanThanh").ToList();

            // Sân được phân công
            ViewBag.DanhSachSan = await _context.SanBongs
                .Where(s => sanIds.Contains(s.Id))
                .ToListAsync();

            ViewBag.SanIds = sanIds;
            ViewBag.Now = now;

            // Badge yêu cầu đổi giờ chưa xử lý (ChoXuLy, chưa qua Staff)
            ViewBag.SoYeuCauDoiGio = await _context.YeuCauDoiGios
                .Include(y => y.DatSan).ThenInclude(d => d.KhungGio)
                .CountAsync(y => sanIds.Contains(y.DatSan.KhungGio.SanBongId)
                              && y.TrangThai == "ChoXuLy"
                              && y.StaffXuLyId == null);

            return View(donHomNay);
        }

        // ══════════════════════════════════════════════════════════
        // 2. CHECK-IN — Tra cứu + chuyển DaXacNhan → DangSuDung
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> CheckIn(string? tuKhoa)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            ViewBag.TuKhoa = tuKhoa;
            ViewBag.KetQua = null;

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                // Tìm theo MaXacNhan hoặc SoDienThoai của User
                var don = await _context.DatSans
                    .Include(d => d.User)
                    .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                    .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                    .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                             && (d.MaXacNhan == tuKhoa.Trim()
                              || d.User.SoDienThoai == tuKhoa.Trim()))
                    .OrderByDescending(d => d.NgayThiDau)
                    .FirstOrDefaultAsync();

                ViewBag.KetQua = don;
                if (don == null)
                    ViewBag.ThongBao = "Không tìm thấy đơn với mã/SĐT này tại sân của bạn.";
            }

            return View();
        }

        // POST: Thực hiện check-in
        [HttpPost]
        public async Task<IActionResult> ThucHienCheckIn(int datSanId)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId));

            if (don == null) return NotFound();

            // Thông báo lý do từ chối rõ ràng theo flow
            if (don.TrangThai != "DaXacNhan")
            {
                var lyDo = don.TrangThai switch
                {
                    "ChoDuyet" => "Đơn đang chờ Owner xác nhận. Hướng dẫn khách liên hệ Owner để được duyệt nhanh.",
                    "DaHuy" => "Đơn này đã bị hủy — không thể check-in.",
                    "DangSuDung" => "Khách đã được check-in rồi.",
                    "HoanThanh" => "Đơn này đã hoàn thành.",
                    _ => $"Đơn ở trạng thái \"{ don.TrangThai }\" — không thể check-in."
                };
                TempData["Error"] = lyDo;
                return RedirectToAction("CheckIn");
            }

            don.TrangThai = "DangSuDung";
            don.StaffCheckInId = GetStaffId();

            // ✅ ĐỒNG BỘ VỚI FLOW USER: Trừ kho dịch vụ đặt trước khi check-in
            // (dịch vụ đặt online không trừ kho ngay — chỉ trừ khi Staff xác nhận giao hàng)
            var dichVuDatTruoc = await _context.DatSanDichVus
                .Include(x => x.DichVu)
                .Where(x => x.DatSanId == datSanId)
                .ToListAsync();

            foreach (var item in dichVuDatTruoc)
            {
                if (item.DichVu != null)
                    item.DichVu.TonKho = Math.Max(0, item.DichVu.TonKho - item.SoLuong);
            }

            // Ghi AuditLog
            _context.AuditLogs.Add(new Web_Stadium.EFCore.AuditLog
            {
                UserId = GetStaffId(),
                VaiTro = "Staff",
                HanhDong = "CheckIn",
                DoiTuong = "DatSan",
                DoiTuongId = datSanId,
                MoTa = $"Check-in đơn {don.MaXacNhan} — {don.User?.HoTen}"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Check-in thành công! {don.User?.HoTen} — {don.KhungGio?.SanBong?.TenSan} | Mã: {don.MaXacNhan}";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════════
        // 3. POS MINI — Thêm dịch vụ vào đơn đang DangSuDung
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> POS(int datSanId)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var don = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId));

            if (don == null) return NotFound();

            // Danh sách dịch vụ của sân đó còn tồn kho
            ViewBag.DanhSachDichVu = await _context.DichVus
                .Where(dv => dv.SanBongId == don.KhungGio.SanBongId
                          && dv.IsActive && dv.TonKho > 0)
                .ToListAsync();

            return View(don);
        }

        // POST: Thêm dịch vụ vào đơn + trừ tồn kho
        [HttpPost]
        public async Task<IActionResult> ThemDichVuPOS(int datSanId, int dichVuId, int soLuong)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var don = await _context.DatSans
                .Include(d => d.KhungGio)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId));
            var dv = await _context.DichVus.FindAsync(dichVuId);

            if (don == null || dv == null) return NotFound();

            if (don.TrangThai != "DangSuDung")
            {
                TempData["Error"] = "Chỉ thêm dịch vụ khi đơn đang ở trạng thái Đang sử dụng!";
                return RedirectToAction("POS", new { datSanId });
            }
            if (dv.TonKho < soLuong)
            {
                TempData["Error"] = $"Tồn kho \"{dv.TenDichVu}\" không đủ! Còn {dv.TonKho} đơn vị.";
                return RedirectToAction("POS", new { datSanId });
            }

            // Kiểm tra dịch vụ đã có trong đơn chưa — nếu có thì cộng thêm
            var daCoTrongDon = await _context.DatSanDichVus
                .FirstOrDefaultAsync(x => x.DatSanId == datSanId && x.DichVuId == dichVuId);

            if (daCoTrongDon != null)
                daCoTrongDon.SoLuong += soLuong;
            else
                _context.DatSanDichVus.Add(new DatSanDichVu
                {
                    DatSanId = datSanId,
                    DichVuId = dichVuId,
                    SoLuong = soLuong
                });

            // Trừ tồn kho
            dv.TonKho -= soLuong;

            // Cộng vào TongTien của đơn
            don.TongTien += dv.Gia * soLuong;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm {soLuong}x \"{dv.TenDichVu}\" vào đơn!";
            return RedirectToAction("POS", new { datSanId });
        }

        // ══════════════════════════════════════════════════════════
        // 4. CHECK-OUT — Thu tiền + chuyển DangSuDung → HoanThanh
        // ══════════════════════════════════════════════════════════
        [HttpGet("Staff/CheckOut/{datSanId}")]
        public async Task<IActionResult> CheckOut(int datSanId)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var don = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId));

            if (don == null) return NotFound();

            // Tính tiền còn lại cần thu
            var tongTienDichVu = don.DatSanDichVus.Sum(x => x.DichVu.Gia * x.SoLuong);
            var tongTatCa = don.KhungGio.Gia + tongTienDichVu;
            ViewBag.TongTienDichVu = tongTienDichVu;
            ViewBag.TongTatCa = tongTatCa;
            ViewBag.ConLai = tongTatCa - don.TienCoc;

            return View(don);
        }

        // POST: Thực hiện check-out
        [HttpPost]
        public async Task<IActionResult> ThucHienCheckOut(int datSanId)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId));

            if (don == null) return NotFound();

            if (don.TrangThai != "DangSuDung")
            {
                TempData["Error"] = $"Đơn #{datSanId} không ở trạng thái Đang sử dụng!";
                return RedirectToAction("Index");
            }

            var tongTienDichVu = don.DatSanDichVus.Sum(x => x.DichVu.Gia * x.SoLuong);
            var tongTatCa = don.KhungGio.Gia + tongTienDichVu;

            don.TrangThai = "HoanThanh";
            don.TongTien = tongTatCa;
            don.StaffCheckOutId = GetStaffId();

            // Giải phóng slot
            var kg = don.KhungGio;
            kg.TrangThai = "Trong";

            // Ghi AuditLog checkout
            _context.AuditLogs.Add(new Web_Stadium.EFCore.AuditLog
            {
                UserId = GetStaffId(),
                VaiTro = "Staff",
                HanhDong = "CheckOut",
                DoiTuong = "DatSan",
                DoiTuongId = datSanId,
                MoTa = $"Check-out đơn {don.MaXacNhan} — Thu {(tongTatCa - don.TienCoc):N0}đ"
            });

            await _context.SaveChangesAsync();

            // BackgroundJobService sẽ tự gửi email mời đánh giá sau 30 phút
            // (kiểm tra AuditLog "GuiMoiDanhGia" để không gửi trùng)

            TempData["Success"] = $"Check-out thành công! Thu {(tongTatCa - don.TienCoc):N0}đ. Tổng đơn: {tongTatCa:N0}đ.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════════
        // 5. BÁO CÁO SỰ CỐ — No-show / Hỏng hóc
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> SuCo()
        {
            var sanIds = await GetSanDuocGiaoAsync();
            // Danh sách đơn có thể báo sự cố (DaXacNhan hoặc DangSuDung)
            ViewBag.DonCoThe = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "DangSuDung")
                         && d.LoaiSuCo == null)
                .OrderByDescending(d => d.NgayThiDau)
                .Take(20).ToListAsync();

            // Lịch sử sự cố gần đây của sân mình
            ViewBag.LichSuSuCo = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.LoaiSuCo != null)
                .OrderByDescending(d => d.ThoiGianTao)
                .Take(10).ToListAsync();

            return View();
        }

        // POST: Ghi nhận sự cố
        [HttpPost]
        public async Task<IActionResult> GhiNhanSuCo(int datSanId, string loaiSuCo, string ghiChu)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var don = await _context.DatSans
                .Include(d => d.KhungGio)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId));

            if (don == null) return NotFound();

            don.LoaiSuCo = loaiSuCo;  // "NoShow" | "HongHoc"
            don.GhiChuSuCo = ghiChu;

            // No-show: khách không đến → chuyển về DaHuy để Owner xem xét
            if (loaiSuCo == "NoShow" && don.TrangThai == "DaXacNhan")
                don.TrangThai = "DaHuy";

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã ghi nhận sự cố \"{loaiSuCo}\" cho đơn #{don.MaXacNhan}. Owner sẽ được thông báo.";
            return RedirectToAction("SuCo");
        }


        // ══════════════════════════════════════════════════════════
        // 6. ĐƠN VÃNG LAI — Bán lẻ độc lập sau khi đơn đã HoanThanh
        // GET /Staff/VangLai
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> VangLai()
        {
            var sanIds = await GetSanDuocGiaoAsync();

            // Tổng hợp dịch vụ của tất cả sân được phân công
            ViewBag.DanhSachDichVu = await _context.DichVus
                .Include(d => d.SanBong)
                .Where(d => sanIds.Contains(d.SanBongId)
                         && d.IsActive && d.TonKho > 0)
                .ToListAsync();

            return View();
        }

        // POST /Staff/ThucHienVangLai
        [HttpPost]
        public async Task<IActionResult> ThucHienVangLai(
            List<int> dichVuIds, List<int> soLuongs)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            if (!dichVuIds.Any())
            {
                TempData["Error"] = "Chưa chọn dịch vụ nào!";
                return RedirectToAction("VangLai");
            }

            decimal tongTien = 0;
            var ghiChu = new List<string>();

            for (int i = 0; i < dichVuIds.Count; i++)
            {
                var sl = (soLuongs != null && i < soLuongs.Count) ? soLuongs[i] : 1;
                if (sl <= 0) continue;

                var dv = await _context.DichVus
                    .FirstOrDefaultAsync(d => d.Id == dichVuIds[i]
                                           && sanIds.Contains(d.SanBongId));
                if (dv == null || dv.TonKho < sl) continue;

                dv.TonKho -= sl;
                tongTien += dv.Gia * sl;
                ghiChu.Add($"{dv.TenDichVu}×{sl}");
            }

            if (tongTien == 0)
            {
                TempData["Error"] = "Không có dịch vụ hợp lệ!";
                return RedirectToAction("VangLai");
            }

            // Ghi AuditLog cho đơn vãng lai (không tạo DatSan mới)
            _context.AuditLogs.Add(new Web_Stadium.EFCore.AuditLog
            {
                UserId = GetStaffId(),
                VaiTro = "Staff",
                HanhDong = "BanVangLai",
                DoiTuong = "VangLai",
                DoiTuongId = 0,
                MoTa = $"Đơn vãng lai: {string.Join(", ", ghiChu)} — Tổng: {tongTien:N0}đ"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Đơn vãng lai hoàn tất! {string.Join(", ", ghiChu)} — Thu {tongTien:N0}đ";
            return RedirectToAction("VangLai");
        }

        // ══════════════════════════════════════════════════════════
        // AJAX: Lấy trạng thái slot realtime (dùng cho dashboard)
        // ══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetTrangThaiSlots()
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var slots = await _context.KhungGios
                .Include(k => k.SanBong)
                .Where(k => sanIds.Contains(k.SanBongId))
                .Select(k => new
                {
                    id = k.Id,
                    sanTen = k.SanBong.TenSan,
                    bat = k.GioBatDau.ToString("HH:mm"),
                    ket = k.GioKetThuc.ToString("HH:mm"),
                    trangThai = k.TrangThai
                }).ToListAsync();
            return Json(slots);
        }

        // ══════════════════════════════════════════════════════════
        // HO SO
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> HoSo(string tab = "tongquan")
        {
            var userId = GetStaffId();
            var user = await _context.Users.FindAsync(userId);

            var sanIds = await _context.StaffSanPhanCongs
                .Where(s => s.StaffId == userId)
                .Select(s => s.SanBongId).ToListAsync();

            var sanPhanCong = await _context.SanBongs
                .Where(s => sanIds.Contains(s.Id)).ToListAsync();

            // Lịch: đơn tại sân phụ trách (7 ngày tới + 30 ngày qua)
            var lichSuCa = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.NgayThiDau >= DateTime.Today.AddDays(-30)
                         && d.NgayThiDau <= DateTime.Today.AddDays(7))
                .OrderByDescending(d => d.NgayThiDau).ToListAsync();

            // Tổng giờ phục vụ thực từ khung giờ
            var tongGio = lichSuCa
                .Where(d => d.KhungGio != null && d.TrangThai == "HoanThanh")
                .Sum(d => (d.KhungGio!.GioKetThuc - d.KhungGio.GioBatDau).TotalHours);

            // Tồn kho các sân phụ trách
            var dichVuBySan = await _context.DichVus
                .Include(d => d.SanBong)
                .Where(d => sanIds.Contains(d.SanBongId) && d.IsActive)
                .GroupBy(d => d.SanBongId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList());

            ViewBag.SanPhanCong = sanPhanCong;
            ViewBag.LichSuCa = lichSuCa;
            ViewBag.SoCheckIn = lichSuCa.Count(d => d.StaffCheckInId == userId);
            ViewBag.SoCheckOut = lichSuCa.Count(d => d.StaffCheckOutId == userId);
            ViewBag.TongGioLam = tongGio;
            ViewBag.SoSuCo = await _context.AuditLogs
                .CountAsync(a => a.UserId == userId && a.HanhDong == "SuCo");
            ViewBag.DichVuBySan = dichVuBySan;
            ViewBag.Tab = tab;
            return View(user);
        }

        // ══════════════════════════════════════════════════════════
        // YÊU CẦU ĐỔI GIỜ — Staff xem và xử lý
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> YeuCauDoiGio()
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var list = await _context.YeuCauDoiGios
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.KhungGioMoi)
                .Where(y => sanIds.Contains(y.DatSan.KhungGio.SanBongId) && y.TrangThai == "ChoXuLy")
                .OrderByDescending(y => y.ThoiGianTao)
                .ToListAsync();
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> XuLyYeuCau(int yeuCauId, string ghiChuStaff, string hanhDong)
        {
            var staffId = GetStaffId();
            var sanIds = await GetSanDuocGiaoAsync();
            var yc = await _context.YeuCauDoiGios
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.KhungGioMoi)
                .FirstOrDefaultAsync(y => y.Id == yeuCauId
                    && sanIds.Contains(y.DatSan.KhungGio.SanBongId)
                    && y.TrangThai == "ChoXuLy");

            if (yc == null) return NotFound();

            yc.StaffXuLyId = staffId;
            yc.GhiChuStaff = ghiChuStaff?.Trim();
            yc.ThoiGianXuLy = DateTime.Now;

            if (hanhDong == "TuChoi")
            {
                yc.TrangThai = "TuChoi";
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = staffId, VaiTro = "Staff", HanhDong = "TuChoiDoiGio",
                    DoiTuong = "YeuCauDoiGio", DoiTuongId = yc.Id,
                    MoTa = $"Từ chối đổi giờ cho đơn {yc.DatSan.MaXacNhan}"
                });
                await _context.SaveChangesAsync();
                var lyDoTuChoi = string.IsNullOrWhiteSpace(ghiChuStaff) ? "Staff từ chối yêu cầu." : ghiChuStaff;
                _ = Task.Run(() => _emailService.GuiEmailDoiGioTuChoi(
                    yc.DatSan.User!.Email, yc.DatSan.User.HoTen ?? "Khách",
                    yc.DatSan.KhungGio.SanBong?.TenSan ?? "", lyDoTuChoi));
                TempData["Success"] = "Đã từ chối yêu cầu và thông báo cho khách.";
            }
            else // ChuyenOwner
            {
                // Chuyển cho Owner — TrangThai vẫn là ChoXuLy, nhưng StaffXuLyId đã có
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = staffId, VaiTro = "Staff", HanhDong = "ChuyenOwnerDoiGio",
                    DoiTuong = "YeuCauDoiGio", DoiTuongId = yc.Id,
                    MoTa = $"Chuyển Owner xử lý đổi giờ cho đơn {yc.DatSan.MaXacNhan}"
                });
                await _context.SaveChangesAsync();
                var san = yc.DatSan.KhungGio.SanBong;
                if (san != null)
                {
                    var owner = await _context.Users.FindAsync(san.OwnerId);
                    if (owner != null && !string.IsNullOrEmpty(owner.Email))
                    {
                        var gioMoi = $"{yc.KhungGioMoi.GioBatDau:hh\\:mm} – {yc.KhungGioMoi.GioKetThuc:hh\\:mm}";
                        _ = Task.Run(() => _emailService.GuiEmailChuyenChoOwner(
                            owner.Email, owner.HoTen ?? "Owner",
                            yc.DatSan.User!.HoTen ?? "Khách", san.TenSan,
                            gioMoi, yc.NgayMoi.ToString("dd/MM/yyyy"),
                            ghiChuStaff ?? ""));
                    }
                }
                TempData["Success"] = "Đã chuyển yêu cầu cho Owner xem xét.";
            }

            return RedirectToAction("YeuCauDoiGio");
        }

        // ══════════════════════════════════════════════════════════
        // GET /Staff/ChuyenNhuong
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> ChuyenNhuong()
        {
            var list = await _context.ChuyenNhuongs
                .Include(c => c.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(c => c.UserA)
                .Include(c => c.UserB)
                .Where(c => c.TrangThai == "ChoStaff")
                .OrderByDescending(c => c.ThoiGianXuLy)
                .ToListAsync();
            return View(list);
        }

        // POST /Staff/XuLyChuyenNhuong
        [HttpPost]
        public async Task<IActionResult> XuLyChuyenNhuong(int chuyenNhuongId, string hanhDong, string? ghiChu)
        {
            var staffId = GetStaffId();
            var cn = await _context.ChuyenNhuongs
                .Include(c => c.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(c => c.UserA)
                .Include(c => c.UserB)
                .FirstOrDefaultAsync(c => c.Id == chuyenNhuongId && c.TrangThai == "ChoStaff");

            if (cn == null) return NotFound();

            cn.StaffXuLyId = staffId;
            cn.GhiChuStaff = ghiChu;
            cn.ThoiGianXuLy = DateTime.Now;

            if (hanhDong == "TuChoi")
            {
                cn.TrangThai = "TuChoi";
                await _context.SaveChangesAsync();

                var san = cn.DatSan?.KhungGio?.SanBong;
                var ngay = cn.DatSan?.NgayThiDau.ToString("dd/MM/yyyy") ?? "";
                var gio = $"{cn.DatSan?.KhungGio?.GioBatDau:HH\\:mm}–{cn.DatSan?.KhungGio?.GioKetThuc:HH\\:mm}";
                foreach (var (email, ten) in new[] {
                    (cn.UserA?.Email ?? "", cn.UserA?.HoTen ?? ""),
                    (cn.UserB?.Email ?? "", cn.UserB?.HoTen ?? "")
                })
                {
                    if (!string.IsNullOrEmpty(email))
                        _ = Task.Run(() => _emailService.GuiEmailChuyenNhuongTuChoi(
                            email, ten, san?.TenSan ?? "", ngay, gio, ghiChu ?? "Không đáp ứng điều kiện"));
                }
                TempData["Success"] = "Đã từ chối yêu cầu chuyển nhượng.";
            }
            else // ChuyenChoOwner
            {
                cn.TrangThai = "ChoOwner";
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã chuyển cho Owner xem xét.";
            }

            return RedirectToAction("ChuyenNhuong");
        }

        // ══════════════════════════════════════════════════════════
        // ⭐ java-recommendation (port 8081) — check-in bằng mã QR
        // Tách biệt với CheckIn()/ThucHienCheckIn() ở trên (tra cứu EFCore trực
        // tiếp) — 2 action mới này đi qua Java, dùng cho luồng quét QR.
        // ══════════════════════════════════════════════════════════

        // GET /Staff/LookupDon?maXacNhan=... — tra cứu đơn đặt sân qua java-recommendation
        [HttpGet]
        public async Task<IActionResult> LookupDon(string maXacNhan)
        {
            var ownerId = await GetOwnerIdCuaStaffAsync();
            if (ownerId == null)
                return Json(new { ok = false, message = "Tài khoản Staff chưa được gán cho Owner nào!" });

            var (ok, message, booking) = await _recommendationService.LookupDatSan(maXacNhan, ownerId.Value, GetJwt());
            return Json(new { ok, message, booking });
        }

        // POST /Staff/CheckInQR — check-in đơn đặt sân qua java-recommendation (QR)
        [HttpPost]
        public async Task<IActionResult> CheckInQR(string maXacNhan)
        {
            var ownerId = await GetOwnerIdCuaStaffAsync();
            if (ownerId == null)
                return Json(new { ok = false, message = "Tài khoản Staff chưa được gán cho Owner nào!" });

            var (ok, message, booking) = await _recommendationService.ScanCheckIn(maXacNhan, ownerId.Value, GetJwt());
            return Json(new { ok, message, booking });
        }
    }
}