using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("User,Owner,Staff,Admin")]
    public class UserController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;

        public UserController(SanBongContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private int GetUserId() => TokenHelper.LayUserId(Request, _config)!.Value;

        // ══════════════════════════════════════════════════════════
        // HỒ SƠ CÁ NHÂN — trang chính với 6 tab
        // GET /User/HoSo?tab=thongtin (mặc định)
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> HoSo(string tab = "thongtin")
        {
            var userId = GetUserId();

            var user = await _context.Users
                .Include(u => u.SanYeuThiches).ThenInclude(s => s.SanBong)
                .Include(u => u.DanhGia).ThenInclude(d => d.SanBong)
                .Include(u => u.DiemThuongLogs)
                .Include(u => u.UserVouchers).ThenInclude(v => v.Voucher)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return RedirectToAction("Login", "Auth");

            // Tab Lịch sử — load riêng với include đầy đủ
            var lichSu = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.ThoiGianTao)
                .ToListAsync();

            // Tab Điểm thưởng — lịch sử giao dịch
            var diemLogs = await _context.DiemThuongLogs
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.ThoiGian)
                .Take(50)
                .ToListAsync();

            // Voucher chưa dùng, chưa hết hạn
            var vouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId && !uv.IsUsed && uv.NgayHetHan > DateTime.Now)
                .OrderBy(uv => uv.NgayHetHan)
                .ToListAsync();

            // Voucher có thể đổi (điểm đủ)
            var vouchersCoTheDoi = await _context.Vouchers
                .Where(v => v.IsActive && v.DiemCanDoi <= user.DiemHienTai)
                .OrderBy(v => v.DiemCanDoi)
                .ToListAsync();

            ViewBag.Tab = tab;
            ViewBag.LichSu = lichSu;
            ViewBag.DiemLogs = diemLogs;
            ViewBag.Vouchers = vouchers;
            ViewBag.VouchersCoTheDoi = vouchersCoTheDoi;

            return View(user);
        }

        // ══════════════════════════════════════════════════════════
        // TAB THÔNG TIN — Cập nhật tên và số điện thoại
        // POST /User/CapNhatThongTin
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> CapNhatThongTin(string hoTen, string? soDienThoai)
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                TempData["Error"] = "Họ tên không được để trống.";
                return RedirectToAction("HoSo", new { tab = "thongtin" });
            }

            user.HoTen = hoTen.Trim();

            // Nếu đổi SĐT → reset xác thực OTP
            if (user.SoDienThoai != soDienThoai?.Trim())
            {
                user.SoDienThoai = soDienThoai?.Trim();
                user.DaXacThucSdt = false;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật thông tin cá nhân!";
            return RedirectToAction("HoSo", new { tab = "thongtin" });
        }

        // ══════════════════════════════════════════════════════════
        // TAB THÔNG TIN — Đổi mật khẩu
        // POST /User/DoiMatKhau
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> DoiMatKhau(string matKhauCu, string matKhauMoi, string xacNhanMatKhau)
        {
            var userId = GetUserId();
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
        // TAB SÂN YÊU THÍCH — Toggle bookmark
        // POST /User/ToggleYeuThich
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> ToggleYeuThich(int sanBongId)
        {
            var userId = GetUserId();

            var existing = await _context.SanYeuThichs  
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SanBongId == sanBongId);

            if (existing != null)
            {
                _context.SanYeuThichs.Remove(existing);
                await _context.SaveChangesAsync();
                return Json(new { liked = false, message = "Đã bỏ yêu thích" });
            }
            else
            {
                _context.SanYeuThichs.Add(new SanYeuThich
                {
                    UserId = userId,
                    SanBongId = sanBongId,
                    NgayThem = DateTime.Now
                });
                await _context.SaveChangesAsync();
                return Json(new { liked = true, message = "Đã thêm vào yêu thích" });
            }
        }

        // API: kiểm tra sân đã được yêu thích chưa (dùng trên trang Venues)
        [HttpGet]
        public async Task<IActionResult> KiemTraYeuThich(int sanBongId)
        {
            var userId = GetUserId();
            var liked = await _context.SanYeuThichs
                .AnyAsync(s => s.UserId == userId && s.SanBongId == sanBongId);
            return Json(new { liked });
        }

        // ══════════════════════════════════════════════════════════
        // TAB ĐIỂM THƯỞNG — Đổi điểm lấy voucher
        // POST /User/DoiDiem
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> DoiDiem(int voucherId)
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var voucher = await _context.Vouchers.FindAsync(voucherId);
            if (voucher == null || !voucher.IsActive)
            {
                TempData["Error"] = "Voucher không tồn tại hoặc đã ngừng hoạt động.";
                return RedirectToAction("HoSo", new { tab = "diem" });
            }

            if (user.DiemHienTai < voucher.DiemCanDoi)
            {
                TempData["Error"] = $"Bạn cần {voucher.DiemCanDoi} điểm để đổi voucher này. Hiện có {user.DiemHienTai} điểm.";
                return RedirectToAction("HoSo", new { tab = "diem" });
            }

            // Trừ điểm
            var diemTruoc = user.DiemHienTai;
            user.DiemHienTai -= voucher.DiemCanDoi;

            // Tạo UserVoucher
            var maSuDung = $"UV-{userId}-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(1000, 9999)}";
            var userVoucher = new UserVoucher
            {
                UserId = userId,
                VoucherId = voucherId,
                MaSuDung = maSuDung,
                NgayDoi = DateTime.Now,
                NgayHetHan = DateTime.Now.AddDays(voucher.SoNgayHieuLuc),
                IsUsed = false
            };
            _context.UserVouchers.Add(userVoucher);

            // Ghi log điểm
            _context.DiemThuongLogs.Add(new DiemThuongLog
            {
                UserId = userId,
                SoDiem = -voucher.DiemCanDoi,
                SoDuSauGd = user.DiemHienTai,
                LoaiSuKien = "DoiVoucher",
                GhiChu = $"Đổi voucher: {voucher.TenVoucher}",
                ThoiGian = DateTime.Now
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đổi thành công! Voucher \"{voucher.TenVoucher}\" đã được thêm vào tài khoản. Mã: {maSuDung}";
            return RedirectToAction("HoSo", new { tab = "diem" });
        }


        // ══════════════════════════════════════════════════════════
        // API: Lấy danh sách sân yêu thích — dùng cho heart dropdown navbar
        // GET /User/GetSanYeuThich
        // ══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetSanYeuThich()
        {
            var userId = GetUserId();

            var list = await _context.SanYeuThichs
                .Include(s => s.SanBong)
                .Where(s => s.UserId == userId && s.SanBong != null
                         && s.SanBong.TrangThaiDuyet == "DaDuyet"
                         && !s.SanBong.IsHidden)
                .OrderByDescending(s => s.NgayThem)
                .Take(10)
                .Select(s => new {
                    id = s.SanBong!.Id,
                    tenSan = s.SanBong.TenSan,
                    quan = s.SanBong.Quan,
                    hinhAnh = s.SanBong.HinhAnh,
                    danhGia = s.SanBong.DanhGiaTrungBinh.ToString("0.0")
                })
                .ToListAsync();

            return Json(list);
        }

        // ══════════════════════════════════════════════════════════
        // UC069 — User tạo Yêu cầu đổi giờ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> TaoYeuCauDoiGio(int datSanId)
        {
            var userId = GetUserId();
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);
            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            { TempData["Error"] = "Chỉ đơn đã xác nhận mới có thể đổi giờ."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }

            var dangCho = await _context.YeuCauDoiGios.AnyAsync(y => y.DatSanId == datSanId && y.TrangThai == "ChoPheDuyet");
            if (dangCho)
            { TempData["Error"] = "Đã tồn tại yêu cầu đổi giờ đang chờ duyệt cho đơn này."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }

            var khungTrong = await _context.KhungGios
                .Where(k => k.SanBongId == don.KhungGio.SanBongId
                         && k.TrangThai == "Trong"
                         && k.Id != don.KhungGioId)
                .OrderBy(k => k.GioBatDau)
                .ToListAsync();

            ViewBag.Don = don;
            ViewBag.KhungTrong = khungTrong;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TaoYeuCauDoiGio(int datSanId, int khungGioMoiId, DateTime ngayThiDauMoi, string lyDo)
        {
            var userId = GetUserId();
            var don = await _context.DatSans
                .Include(d => d.KhungGio)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);
            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            { TempData["Error"] = "Đơn không hợp lệ."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }
            if (string.IsNullOrWhiteSpace(lyDo))
            { TempData["Error"] = "Vui lòng nhập lý do."; return RedirectToAction("TaoYeuCauDoiGio", new { datSanId }); }

            var khung = await _context.KhungGios.FirstOrDefaultAsync(k => k.Id == khungGioMoiId);
            if (khung == null || khung.SanBongId != don.KhungGio.SanBongId || khung.TrangThai != "Trong")
            { TempData["Error"] = "Khung giờ mới không hợp lệ."; return RedirectToAction("TaoYeuCauDoiGio", new { datSanId }); }

            _context.YeuCauDoiGios.Add(new YeuCauDoiGio
            {
                DatSanId = datSanId,
                KhungGioMoiId = khungGioMoiId,
                NgayThiDauMoi = ngayThiDauMoi,
                LyDo = lyDo.Trim(),
                TrangThai = "ChoPheDuyet",
                NgayTao = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi yêu cầu đổi giờ. Vui lòng chờ chủ sân duyệt.";
            return RedirectToAction("HoSo", new { tab = "lichsu" });
        }

        // ══════════════════════════════════════════════════════════
        // UC070 — User tạo Yêu cầu đổi sân
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> TaoYeuCauDoiSan(int datSanId)
        {
            var userId = GetUserId();
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);
            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            { TempData["Error"] = "Chỉ đơn đã xác nhận mới có thể đổi sân."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }

            var dangCho = await _context.YeuCauDoiSans.AnyAsync(y => y.DatSanId == datSanId && y.TrangThai == "ChoPheDuyet");
            if (dangCho)
            { TempData["Error"] = "Đã tồn tại yêu cầu đổi sân đang chờ duyệt."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }

            // Sân khác đã duyệt + cùng owner thì user có thể yêu cầu chuyển sang
            var ownerId = don.KhungGio.SanBong.OwnerId;
            var sanKhac = await _context.SanBongs
                .Where(s => s.OwnerId == ownerId && s.TrangThaiDuyet == "DaDuyet" && s.Id != don.KhungGio.SanBongId)
                .Select(s => new { s.Id, s.TenSan })
                .ToListAsync();

            ViewBag.Don = don;
            ViewBag.SanKhac = sanKhac;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TaoYeuCauDoiSan(int datSanId, int khungGioMoiId, DateTime ngayThiDauMoi, string lyDo)
        {
            var userId = GetUserId();
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);
            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            { TempData["Error"] = "Đơn không hợp lệ."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }
            if (string.IsNullOrWhiteSpace(lyDo))
            { TempData["Error"] = "Vui lòng nhập lý do."; return RedirectToAction("TaoYeuCauDoiSan", new { datSanId }); }

            var khung = await _context.KhungGios.Include(k => k.SanBong)
                .FirstOrDefaultAsync(k => k.Id == khungGioMoiId);
            if (khung == null || khung.TrangThai != "Trong")
            { TempData["Error"] = "Khung giờ mới không khả dụng."; return RedirectToAction("TaoYeuCauDoiSan", new { datSanId }); }

            // Sân mới phải cùng Owner để Owner có quyền duyệt
            if (khung.SanBong.OwnerId != don.KhungGio.SanBong.OwnerId)
            { TempData["Error"] = "Chỉ có thể đổi sang sân khác của cùng chủ sân."; return RedirectToAction("TaoYeuCauDoiSan", new { datSanId }); }

            _context.YeuCauDoiSans.Add(new YeuCauDoiSan
            {
                DatSanId = datSanId,
                KhungGioMoiId = khungGioMoiId,
                NgayThiDauMoi = ngayThiDauMoi,
                LyDo = lyDo.Trim(),
                TrangThai = "ChoPheDuyet",
                NgayTao = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi yêu cầu đổi sân.";
            return RedirectToAction("HoSo", new { tab = "lichsu" });
        }

        // API: trả khung giờ trống cho 1 sân — phục vụ dropdown động trang đổi sân
        public async Task<IActionResult> KhungTrongTheoSan(int sanBongId)
        {
            var raw = await _context.KhungGios
                .Where(k => k.SanBongId == sanBongId && k.TrangThai == "Trong")
                .OrderBy(k => k.GioBatDau)
                .ToListAsync();
            var list = raw.Select(k => new {
                k.Id,
                GioBatDau = k.GioBatDau.ToString("HH:mm"),
                GioKetThuc = k.GioKetThuc.ToString("HH:mm"),
                k.Gia
            }).ToList();
            return Json(list);
        }

        // ══════════════════════════════════════════════════════════
        // UC071 — User tạo Yêu cầu chuyển nhượng
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> TaoChuyenNhuong(int datSanId)
        {
            var userId = GetUserId();
            var don = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);
            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            { TempData["Error"] = "Chỉ đơn đã xác nhận mới có thể chuyển nhượng."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }

            var dangCho = await _context.ChuyenNhuongDatSans.AnyAsync(c => c.DatSanId == datSanId && c.TrangThai == "ChoPheDuyet");
            if (dangCho)
            { TempData["Error"] = "Đã tồn tại yêu cầu chuyển nhượng đang chờ duyệt."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }

            ViewBag.Don = don;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TaoChuyenNhuong(int datSanId, string? emailNguoiNhan, string? sdtNguoiNhan, string lyDo)
        {
            var userId = GetUserId();
            var don = await _context.DatSans
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == datSanId && d.UserId == userId);
            if (don == null) return NotFound();
            if (don.TrangThai != "DaXacNhan")
            { TempData["Error"] = "Đơn không hợp lệ."; return RedirectToAction("HoSo", new { tab = "lichsu" }); }
            if (string.IsNullOrWhiteSpace(lyDo))
            { TempData["Error"] = "Vui lòng nhập lý do."; return RedirectToAction("TaoChuyenNhuong", new { datSanId }); }

            emailNguoiNhan = emailNguoiNhan?.Trim();
            sdtNguoiNhan = sdtNguoiNhan?.Trim();

            if (string.IsNullOrWhiteSpace(emailNguoiNhan) && string.IsNullOrWhiteSpace(sdtNguoiNhan))
            { TempData["Error"] = "Cần nhập email hoặc số điện thoại người nhận."; return RedirectToAction("TaoChuyenNhuong", new { datSanId }); }

            // Tự kiểm tra trùng người chuyển ngay client side
            if (!string.IsNullOrWhiteSpace(emailNguoiNhan) && emailNguoiNhan == don.User.Email)
            { TempData["Error"] = "Không thể chuyển nhượng cho chính mình."; return RedirectToAction("TaoChuyenNhuong", new { datSanId }); }

            // Resolve trước (nếu tìm thấy) để hiển thị cho Owner
            int? nguoiNhanId = null;
            if (!string.IsNullOrWhiteSpace(emailNguoiNhan))
                nguoiNhanId = (await _context.Users.FirstOrDefaultAsync(u => u.Email == emailNguoiNhan && u.IsActive))?.Id;
            if (nguoiNhanId == null && !string.IsNullOrWhiteSpace(sdtNguoiNhan))
                nguoiNhanId = (await _context.Users.FirstOrDefaultAsync(u => u.SoDienThoai == sdtNguoiNhan && u.IsActive))?.Id;

            _context.ChuyenNhuongDatSans.Add(new ChuyenNhuongDatSan
            {
                DatSanId = datSanId,
                NguoiChuyenId = userId,
                EmailNguoiNhan = emailNguoiNhan,
                SdtNguoiNhan = sdtNguoiNhan,
                NguoiNhanId = nguoiNhanId,
                LyDo = lyDo.Trim(),
                TrangThai = "ChoPheDuyet",
                NgayTao = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = nguoiNhanId.HasValue
                ? "Đã gửi yêu cầu chuyển nhượng. Đang chờ chủ sân duyệt."
                : "Đã gửi yêu cầu. Lưu ý: hệ thống chưa tìm thấy tài khoản người nhận — họ cần đăng ký trước khi chủ sân duyệt.";
            return RedirectToAction("HoSo", new { tab = "lichsu" });
        }

        // ══════════════════════════════════════════════════════════
        // CỘNG ĐIỂM — Gọi nội bộ sau khi đánh giá / đặt sân
        // Dùng static method để BookingController / DanhGiaController gọi được
        // ══════════════════════════════════════════════════════════
        public static async Task CongDiem(SanBongContext context, int userId,
            int soDiem, string loaiSuKien, string ghiChu, int? datSanId = null)
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null) return;

            user.DiemHienTai += soDiem;

            context.DiemThuongLogs.Add(new DiemThuongLog
            {
                UserId = userId,
                SoDiem = soDiem,
                SoDuSauGd = user.DiemHienTai,
                LoaiSuKien = loaiSuKien,
                GhiChu = ghiChu,
                DatSanId = datSanId,
                ThoiGian = DateTime.Now
            });

            await context.SaveChangesAsync();
        }
    }
}