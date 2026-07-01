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
                .Take(50)
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

            // DatSanIds đã có bài đăng chuyển nhượng (còn active hoặc hoàn tất)
            var datSanIds = lichSu.Select(d => d.Id).ToList();
            var daCoCn = await _context.ChuyenNhuongs
                .Where(c => datSanIds.Contains(c.DatSanId)
                    && c.TrangThai != "TuChoi"
                    && c.TrangThai != "DaHuy")
                .Select(c => c.DatSanId)
                .Distinct()
                .ToListAsync();
            ViewBag.DatSanDaCoCn = daCoCn.ToHashSet();

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