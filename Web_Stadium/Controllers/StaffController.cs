using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Hubs;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Staff")]
    public class StaffController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly IHubContext<SanBongHub> _hub;

        public StaffController(SanBongContext context, IConfiguration config, IHubContext<SanBongHub> hub)
        {
            _context = context;
            _config = config;
            _hub = hub;
        }

        private Task PhatSuKienAsync(int sanId, string loai, object payload)
            => _hub.Clients.Group($"san_{sanId}").SendAsync("StaffSuKien", new {
                loai, sanId, thoiGian = DateTime.Now.ToString("HH:mm:ss"), data = payload
            });

        private int GetStaffId() => TokenHelper.LayUserId(Request, _config)!.Value;

        private async Task<List<int>> GetSanDuocGiaoAsync()
        {
            var staffId = GetStaffId();
            return await _context.StaffSanPhanCongs
                .Where(p => p.StaffId == staffId)
                .Select(p => p.SanBongId)
                .ToListAsync();
        }

        // ══════════════════════════════════════════════════════════
        // 1. DASHBOARD CA TRỰC
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            var staffId = GetStaffId();
            var sanIds  = await GetSanDuocGiaoAsync();
            var now     = DateTime.Now;

            var donHomNay = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.NgayThiDau.Date == now.Date
                         && d.TrangThai != "DaHuy")
                .OrderBy(d => d.KhungGio.GioBatDau)
                .ToListAsync();

            var hoanThanh = donHomNay.Where(d => d.TrangThai == "HoanThanh").ToList();

            ViewBag.DangDien  = donHomNay.Where(d => d.TrangThai == "DangSuDung").ToList();
            ViewBag.SapDen    = donHomNay.Where(d => d.TrangThai == "DaXacNhan").ToList();
            ViewBag.HoanThanh = hoanThanh;

            ViewBag.DanhSachSan = await _context.SanBongs
                .Where(s => sanIds.Contains(s.Id))
                .ToListAsync();

            ViewBag.NhatKyCa = await _context.AuditLogs
                .Where(a => a.UserId == staffId && a.ThoiGian.Date == now.Date)
                .OrderByDescending(a => a.ThoiGian)
                .Take(30)
                .ToListAsync();

            ViewBag.SuCoHomNay = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.NgayThiDau.Date == now.Date
                         && d.LoaiSuCo != null)
                .OrderByDescending(d => d.ThoiGianTao)
                .ToListAsync();

            ViewBag.DoanhThuCa = hoanThanh.Sum(d => d.TongTien - d.TienCoc);
            ViewBag.SoCheckinCa  = donHomNay.Count(d => d.StaffCheckInId == staffId);
            ViewBag.SoCheckoutCa = donHomNay.Count(d => d.StaffCheckOutId == staffId);

            ViewBag.SanIds    = sanIds;
            ViewBag.Now       = now;
            ViewBag.TenStaff  = (await _context.Users.FindAsync(staffId))?.HoTen ?? "Staff";

            return View(donHomNay);
        }

        // ══════════════════════════════════════════════════════════
        // 2. CHECK-IN
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> CheckIn(string? tuKhoa)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            ViewBag.SanIds = sanIds;
            ViewBag.TuKhoa = tuKhoa;
            ViewBag.KetQua = null;

            var lichHomNay = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.NgayThiDau.Date == DateTime.Today
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "DangSuDung"))
                .OrderBy(d => d.KhungGio.GioBatDau)
                .ToListAsync();

            ViewBag.SapDen   = lichHomNay.Where(d => d.TrangThai == "DaXacNhan").ToList();
            ViewBag.DangDien = lichHomNay.Where(d => d.TrangThai == "DangSuDung").ToList();

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
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

                if (don != null)
                    ViewBag.LichSuKhach = await _context.DatSans
                        .Where(d => d.UserId == don.UserId && d.Id != don.Id)
                        .OrderByDescending(d => d.NgayThiDau)
                        .CountAsync();
            }

            return View();
        }

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

            if (don.TrangThai != "DaXacNhan")
            {
                var lyDo = don.TrangThai switch
                {
                    "ChoDuyet"   => "Đơn đang chờ Owner xác nhận. Hướng dẫn khách liên hệ Owner để được duyệt nhanh.",
                    "DaHuy"      => "Đơn này đã bị hủy — không thể check-in.",
                    "DangSuDung" => "Khách đã được check-in rồi.",
                    "HoanThanh"  => "Đơn này đã hoàn thành.",
                    _            => $"Đơn ở trạng thái \"{don.TrangThai}\" — không thể check-in."
                };
                TempData["Error"] = lyDo;
                return RedirectToAction("CheckIn");
            }

            var staffId = GetStaffId();
            don.TrangThai = "DangSuDung";
            don.StaffCheckInId = staffId;

            var dichVuDatTruoc = await _context.DatSanDichVus
                .Include(x => x.DichVu)
                .Where(x => x.DatSanId == datSanId)
                .ToListAsync();

            foreach (var item in dichVuDatTruoc)
            {
                if (item.DichVu != null)
                    item.DichVu.TonKho = Math.Max(0, item.DichVu.TonKho - item.SoLuong);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId    = staffId,
                VaiTro    = "Staff",
                HanhDong  = "CheckIn",
                DoiTuong  = "DatSan",
                DoiTuongId = datSanId,
                MoTa      = $"Check-in đơn {don.MaXacNhan} — {don.User?.HoTen}"
            });

            await _context.SaveChangesAsync();

            await PhatSuKienAsync(don.KhungGio.SanBongId, "CheckIn", new {
                donId = don.Id, maXacNhan = don.MaXacNhan
            });

            TempData["Success"] = $"✅ Check-in thành công! {don.User?.HoTen} — {don.KhungGio?.SanBong?.TenSan} | Mã: {don.MaXacNhan}";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════════
        // 3. POS MINI
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> POS(int id)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            ViewBag.SanIds = sanIds;
            var don = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == id
                                       && sanIds.Contains(d.KhungGio.SanBongId));

            if (don == null) return NotFound();

            ViewBag.DanhSachDichVu = await _context.DichVus
                .Where(dv => dv.SanBongId == don.KhungGio.SanBongId
                          && dv.IsActive && dv.TonKho > 0)
                .ToListAsync();

            return View(don);
        }

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
                return RedirectToAction("POS", new { id = datSanId });
            }
            if (dv.TonKho < soLuong)
            {
                TempData["Error"] = $"Tồn kho \"{dv.TenDichVu}\" không đủ! Còn {dv.TonKho} đơn vị.";
                return RedirectToAction("POS", new { id = datSanId });
            }

            var daCoTrongDon = await _context.DatSanDichVus
                .FirstOrDefaultAsync(x => x.DatSanId == datSanId && x.DichVuId == dichVuId);

            if (daCoTrongDon != null)
                daCoTrongDon.SoLuong += soLuong;
            else
                _context.DatSanDichVus.Add(new DatSanDichVu
                {
                    DatSanId  = datSanId,
                    DichVuId  = dichVuId,
                    SoLuong   = soLuong
                });

            dv.TonKho    -= soLuong;
            don.TongTien += dv.Gia * soLuong;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm {soLuong}x \"{dv.TenDichVu}\" vào đơn!";
            return RedirectToAction("POS", new { id = datSanId });
        }

        // ══════════════════════════════════════════════════════════
        // 4. CHECK-OUT
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> CheckOut(int id)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            ViewBag.SanIds = sanIds;
            var don = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == id
                                       && sanIds.Contains(d.KhungGio.SanBongId));

            if (don == null) return NotFound();

            var tongTienDichVu = don.DatSanDichVus.Sum(x => x.DichVu.Gia * x.SoLuong);
            var tongTatCa = don.KhungGio.Gia + tongTienDichVu;
            ViewBag.TongTienDichVu = tongTienDichVu;
            ViewBag.TongTatCa      = tongTatCa;
            ViewBag.ConLai         = tongTatCa - don.TienCoc;

            ViewBag.BankName      = _config["Payment:BankName"];
            ViewBag.AccountNumber = _config["Payment:AccountNumber"];
            ViewBag.AccountName   = _config["Payment:AccountName"];

            return View(don);
        }

        [HttpPost]
        public async Task<IActionResult> ThucHienCheckOut(int datSanId, string? phuongThuc = "cash")
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

            var staffId = GetStaffId();
            don.TrangThai      = "HoanThanh";
            don.TongTien       = tongTatCa;
            don.StaffCheckOutId = staffId;

            var kg = don.KhungGio;
            kg.TrangThai = "Trong";

            _context.AuditLogs.Add(new AuditLog
            {
                UserId     = staffId,
                VaiTro     = "Staff",
                HanhDong   = "CheckOut",
                DoiTuong   = "DatSan",
                DoiTuongId = datSanId,
                MoTa       = $"Check-out đơn {don.MaXacNhan} — Thu {(tongTatCa - don.TienCoc):N0}đ"
            });

            await _context.SaveChangesAsync();

            await PhatSuKienAsync(kg.SanBongId, "CheckOut", new {
                donId = don.Id, maXacNhan = don.MaXacNhan, tongTien = tongTatCa
            });

            var ptLabel = phuongThuc == "bank" ? "Chuyển khoản" : "Tiền mặt";
            TempData["Success"] = $"Check-out thành công! Thu {(tongTatCa - don.TienCoc):N0}đ qua {ptLabel}. Tổng đơn: {tongTatCa:N0}đ.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════════
        // 5. BÁO CÁO SỰ CỐ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> SuCo()
        {
            var sanIds = await GetSanDuocGiaoAsync();
            ViewBag.SanIds = sanIds;
            ViewBag.DonCoThe = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "DangSuDung")
                         && d.LoaiSuCo == null)
                .OrderByDescending(d => d.NgayThiDau)
                .Take(20).ToListAsync();

            ViewBag.LichSuSuCo = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.LoaiSuCo != null)
                .OrderByDescending(d => d.ThoiGianTao)
                .Take(10).ToListAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GhiNhanSuCo(int datSanId, string loaiSuCo, string ghiChu)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var don = await _context.DatSans
                .Include(d => d.KhungGio)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId));

            if (don == null) return NotFound();

            don.LoaiSuCo   = loaiSuCo;
            don.GhiChuSuCo = ghiChu;

            if (loaiSuCo == "NoShow" && don.TrangThai == "DaXacNhan")
                don.TrangThai = "DaHuy";

            _context.AuditLogs.Add(new AuditLog
            {
                UserId     = GetStaffId(),
                VaiTro     = "Staff",
                HanhDong   = "SuCo",
                DoiTuong   = "DatSan",
                DoiTuongId = datSanId,
                MoTa       = $"Sự cố {loaiSuCo} — đơn {don.MaXacNhan}"
                             + (string.IsNullOrEmpty(ghiChu) ? "" : $": {ghiChu}")
            });

            await _context.SaveChangesAsync();

            await PhatSuKienAsync(don.KhungGio.SanBongId, "SuCo", new {
                donId = don.Id, maXacNhan = don.MaXacNhan, loaiSuCo, ghiChu
            });

            TempData["Success"] = $"Đã ghi nhận sự cố \"{loaiSuCo}\" cho đơn #{don.MaXacNhan}. Owner sẽ được thông báo.";
            return RedirectToAction("SuCo");
        }

        // ══════════════════════════════════════════════════════════
        // 6. ĐƠN VÃNG LAI
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> VangLai()
        {
            var sanIds = await GetSanDuocGiaoAsync();

            ViewBag.DanhSachDichVu = await _context.DichVus
                .Include(d => d.SanBong)
                .Where(d => sanIds.Contains(d.SanBongId)
                         && d.IsActive && d.TonKho > 0)
                .ToListAsync();

            return View();
        }

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
                tongTien  += dv.Gia * sl;
                ghiChu.Add($"{dv.TenDichVu}×{sl}");
            }

            if (tongTien == 0)
            {
                TempData["Error"] = "Không có dịch vụ hợp lệ!";
                return RedirectToAction("VangLai");
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId     = GetStaffId(),
                VaiTro     = "Staff",
                HanhDong   = "BanVangLai",
                DoiTuong   = "VangLai",
                DoiTuongId = 0,
                MoTa       = $"Đơn vãng lai: {string.Join(", ", ghiChu)} — Tổng: {tongTien:N0}đ"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Đơn vãng lai hoàn tất! {string.Join(", ", ghiChu)} — Thu {tongTien:N0}đ";
            return RedirectToAction("VangLai");
        }

        // ══════════════════════════════════════════════════════════
        // 7. GHI CHÚ NHANH
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> LuuGhiChu(int datSanId, string? ghiChu)
        {
            var sanIds = await GetSanDuocGiaoAsync();
            var don = await _context.DatSans
                .Include(d => d.KhungGio)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId));
            if (don == null) return NotFound();

            don.GhiChuStaff = ghiChu?.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        // ══════════════════════════════════════════════════════════
        // AJAX: Trạng thái slot realtime
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
                    id       = k.Id,
                    sanTen   = k.SanBong.TenSan,
                    bat      = k.GioBatDau.ToString(@"hh\:mm"),
                    ket      = k.GioKetThuc.ToString(@"hh\:mm"),
                    trangThai = k.TrangThai
                }).ToListAsync();
            return Json(slots);
        }

        // ══════════════════════════════════════════════════════════
        // CẬP NHẬT THÔNG TIN / ĐỔI MẬT KHẨU (dùng bởi HoSo.cshtml)
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> CapNhatThongTin(string hoTen, string? soDienThoai)
        {
            var userId = GetStaffId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                TempData["Error"] = "Họ tên không được để trống.";
                return RedirectToAction("HoSo", new { tab = "thongtin" });
            }

            user.HoTen = hoTen.Trim();
            if (user.SoDienThoai != soDienThoai?.Trim())
            {
                user.SoDienThoai = soDienThoai?.Trim();
                user.DaXacThucSdt = false;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật thông tin cá nhân!";
            return RedirectToAction("HoSo", new { tab = "thongtin" });
        }

        [HttpPost]
        public async Task<IActionResult> DoiMatKhau(string matKhauCu, string matKhauMoi, string xacNhanMatKhau)
        {
            var userId = GetStaffId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(matKhauCu, user.MatKhau))
            {
                TempData["Error"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction("HoSo", new { tab = "baomat" });
            }
            if (matKhauMoi != xacNhanMatKhau)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("HoSo", new { tab = "baomat" });
            }
            if (matKhauMoi.Length < 6)
            {
                TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return RedirectToAction("HoSo", new { tab = "baomat" });
            }

            user.MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhauMoi);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("HoSo", new { tab = "baomat" });
        }

        // ══════════════════════════════════════════════════════════
        // HỒ SƠ NHÂN VIÊN
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> HoSo(string tab = "tongquan")
        {
            var userId = GetStaffId();
            var user   = await _context.Users.FindAsync(userId);

            var sanIds = await _context.StaffSanPhanCongs
                .Where(s => s.StaffId == userId)
                .Select(s => s.SanBongId).ToListAsync();

            var sanPhanCong = await _context.SanBongs
                .Where(s => sanIds.Contains(s.Id)).ToListAsync();

            var lichSuCa = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.NgayThiDau >= DateTime.Today.AddDays(-30)
                         && d.NgayThiDau <= DateTime.Today.AddDays(7))
                .OrderByDescending(d => d.NgayThiDau).ToListAsync();

            var tongGio = lichSuCa
                .Where(d => d.KhungGio != null && d.TrangThai == "HoanThanh")
                .Sum(d => (d.KhungGio!.GioKetThuc - d.KhungGio.GioBatDau).TotalHours);

            var dichVuBySan = await _context.DichVus
                .Include(d => d.SanBong)
                .Where(d => sanIds.Contains(d.SanBongId) && d.IsActive)
                .GroupBy(d => d.SanBongId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList());

            ViewBag.SanPhanCong  = sanPhanCong;
            ViewBag.LichSuCa     = lichSuCa;
            ViewBag.SoCheckIn    = lichSuCa.Count(d => d.StaffCheckInId == userId);
            ViewBag.SoCheckOut   = lichSuCa.Count(d => d.StaffCheckOutId == userId);
            ViewBag.TongGioLam   = tongGio;
            ViewBag.SoSuCo       = await _context.AuditLogs
                .CountAsync(a => a.UserId == userId && a.HanhDong == "SuCo");
            ViewBag.DichVuBySan  = dichVuBySan;
            ViewBag.Tab          = tab;
            return View(user);
        }
    }
}
