using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    public class TournamentPublicController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        private readonly StandingService _standingService;

        public TournamentPublicController(
            SanBongContext context,
            IConfiguration config,
            EmailService emailService,
            StandingService standingService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
            _standingService = standingService;
        }

        private int? GetUserId() => TokenHelper.LayUserId(Request, _config);

        // ══════════════════════════════════════════════════════════
        // GET /TournamentPublic — Tìm kiếm giải đấu công khai
        // Bộ lọc: tên, sân, quận, trạng thái, lệ phí
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index(
            string? keyword, string? quan, string? trangThai,
            decimal? lePhiTu, decimal? lePhiDen, string? sapXep)
        {
            var query = _context.GiaiDaus
                .Include(g => g.SanBong)
                .Include(g => g.DoiBongs)
                .Where(g => g.TrangThai != "Draft"); // Ẩn Draft khỏi public

            // Bộ lọc tên / sân
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(g =>
                    g.TenGiai.Contains(keyword) ||
                    g.SanBong.TenSan.Contains(keyword) ||
                    (g.MoTa != null && g.MoTa.Contains(keyword)));

            // Lọc quận
            if (!string.IsNullOrEmpty(quan))
                query = query.Where(g => g.SanBong.Quan == quan);

            // Lọc trạng thái
            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(g => g.TrangThai == trangThai);

            // Lọc lệ phí
            if (lePhiTu.HasValue)
                query = query.Where(g => g.LePhiGiai >= lePhiTu.Value);
            if (lePhiDen.HasValue)
                query = query.Where(g => g.LePhiGiai <= lePhiDen.Value);

            // Sắp xếp
            query = sapXep switch
            {
                "lephi_tang" => query.OrderBy(g => g.LePhiGiai),
                "lephi_giam" => query.OrderByDescending(g => g.LePhiGiai),
                "ngay_gan" => query.OrderBy(g => g.NgayBatDau),
                "moi_nhat" => query.OrderByDescending(g => g.ThoiGianTao),
                _ => query.OrderByDescending(g => g.TrangThai == "RegistrationOpen")
                                      .ThenByDescending(g => g.NgayBatDau)
            };

            var giaiList = await query.ToListAsync();

            // Filter data cho dropdown
            ViewBag.DanhSachQuan = await _context.DanhMucQuans
                .Where(q => q.IsActive).OrderBy(q => q.ThuTu).ToListAsync();

            // Lưu lại giá trị filter
            ViewBag.Keyword = keyword;
            ViewBag.Quan = quan;
            ViewBag.TrangThai = trangThai;
            ViewBag.LePhiTu = lePhiTu;
            ViewBag.LePhiDen = lePhiDen;
            ViewBag.SapXep = sapXep;

            return View(giaiList);
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentPublic/Details/5 — Chi tiết giải công khai
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Details(int id)
        {
            var giaiDau = await _context.GiaiDaus
                .Include(g => g.SanBong).ThenInclude(s => s.AnhSanBongs)
                .Include(g => g.Owner)
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs).ThenInclude(d => d.ThanhViens)
                .Include(g => g.DoiBongs).ThenInclude(d => d.Bang)
                .Include(g => g.DoiBongs).ThenInclude(d => d.DoiTruong)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiNha)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiKhach)
                .Include(g => g.TranDaus).ThenInclude(t => t.SuKiens)
                    .ThenInclude(s => s.ThanhVien)
                .FirstOrDefaultAsync(g => g.Id == id && g.TrangThai != "Draft");

            if (giaiDau == null) return NotFound();

            var userId = GetUserId();

            // Kiểm tra user đã đăng ký chưa
            ViewBag.DoiCuaToi = userId.HasValue
                ? giaiDau.DoiBongs.FirstOrDefault(d => d.DoiTruongId == userId.Value)
                : null;

            // BXH
            ViewBag.BangXepHang = await _standingService.GetStandings(id);

            // Vua phá lưới
            ViewBag.VuaPhaLuoi = giaiDau.DoiBongs
                .SelectMany(d => d.ThanhViens)
                .Where(tv => tv.TongBanThang > 0)
                .OrderByDescending(tv => tv.TongBanThang)
                .Take(10).ToList();

            ViewBag.UserId = userId;

            return View(giaiDau);
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentPublic/DangKy/5 — Form đăng ký đội
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap("User,Owner,Staff,Admin")]
        public async Task<IActionResult> DangKy(int id)
        {
            var giaiDau = await _context.GiaiDaus
                .Include(g => g.SanBong)
                .Include(g => g.DoiBongs)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (giaiDau == null) return NotFound();

            if (giaiDau.TrangThai != "RegistrationOpen")
            {
                TempData["Error"] = "Giải đấu không trong thời gian đăng ký!";
                return RedirectToAction("Details", new { id });
            }

            // Kiểm tra đã đăng ký chưa
            var userId = GetUserId()!.Value;
            var daDangKy = await _context.DoiBongs
                .AnyAsync(d => d.GiaiDauId == id && d.DoiTruongId == userId);
            if (daDangKy)
            {
                TempData["Error"] = "Bạn đã đăng ký giải này rồi!";
                return RedirectToAction("Details", new { id });
            }

            // Kiểm tra còn slot
            var soDoiDaDK = giaiDau.DoiBongs.Count(d => d.DaThanhToan);
            if (soDoiDaDK >= giaiDau.SoDoiToiDa)
            {
                TempData["Error"] = "Giải đấu đã đủ đội!";
                return RedirectToAction("Details", new { id });
            }

            ViewBag.GiaiDau = giaiDau;
            ViewBag.SlotConLai = giaiDau.SoDoiToiDa - soDoiDaDK;
            return View(giaiDau);
        }

        // ══════════════════════════════════════════════════════════
        // POST /TournamentPublic/XacNhanDangKy — Tạo đội + chờ thanh toán
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        [YeuCauDangNhap("User,Owner,Staff,Admin")]
        public async Task<IActionResult> XacNhanDangKy(int giaiDauId, string tenDoi)
        {
            var userId = GetUserId()!.Value;
            var giaiDau = await _context.GiaiDaus
                .Include(g => g.DoiBongs)
                .FirstOrDefaultAsync(g => g.Id == giaiDauId);

            if (giaiDau == null || giaiDau.TrangThai != "RegistrationOpen")
            {
                TempData["Error"] = "Giải đấu không hợp lệ!";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(tenDoi))
            {
                TempData["Error"] = "Tên đội không được để trống!";
                return RedirectToAction("DangKy", new { id = giaiDauId });
            }

            // Kiểm tra trùng tên đội trong giải
            var tenTrung = await _context.DoiBongs
                .AnyAsync(d => d.GiaiDauId == giaiDauId && d.TenDoi == tenDoi.Trim());
            if (tenTrung)
            {
                TempData["Error"] = "Tên đội đã tồn tại trong giải này!";
                return RedirectToAction("DangKy", new { id = giaiDauId });
            }

            // Tạo đội — chưa thanh toán
            var doi = new DoiBong
            {
                GiaiDauId = giaiDauId,
                DoiTruongId = userId,
                TenDoi = tenDoi.Trim(),
                TienKyQuyConLai = giaiDau.TienKyQuy,
                DaThanhToan = false,
                TrangThai = "Active",
                ThoiGianTao = DateTime.Now
            };
            _context.DoiBongs.Add(doi);
            await _context.SaveChangesAsync();

            // Redirect sang trang checkout 15 phút
            return RedirectToAction("Checkout", new { doiId = doi.Id });
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentPublic/Checkout/{doiId} — Thanh toán 15 phút
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap("User,Owner,Staff,Admin")]
        public async Task<IActionResult> Checkout(int doiId)
        {
            var userId = GetUserId()!.Value;
            var doi = await _context.DoiBongs
                .Include(d => d.GiaiDau).ThenInclude(g => g.SanBong)
                .FirstOrDefaultAsync(d => d.Id == doiId && d.DoiTruongId == userId);

            if (doi == null) return NotFound();
            if (doi.DaThanhToan)
                return RedirectToAction("NopDanhSach", new { doiId });

            // Tính thời gian còn lại (15 phút từ khi tạo đội)
            var hetHan = doi.ThoiGianTao.AddMinutes(15);
            var conLaiGiay = Math.Max(0, (int)(hetHan - DateTime.Now).TotalSeconds);

            if (conLaiGiay <= 0)
            {
                // Hết giờ — xóa đội
                _context.DoiBongs.Remove(doi);
                await _context.SaveChangesAsync();
                TempData["Error"] = "Hết thời gian thanh toán! Vui lòng đăng ký lại.";
                return RedirectToAction("Details", new { id = doi.GiaiDauId });
            }

            ViewBag.ConLaiGiay = conLaiGiay;
            ViewBag.TongThanhToan = doi.GiaiDau.LePhiGiai + doi.GiaiDau.TienKyQuy;
            return View(doi);
        }

        // XacNhanThanhToan đã được chuyển sang Owner
        // User chỉ chuyển khoản tay → Owner xác nhận trên hệ thống
        // Xem TournamentController.XacNhanThanhToanDoi()

        // ══════════════════════════════════════════════════════════
        // GET /TournamentPublic/NopDanhSach/{doiId} — Nhập thành viên
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap("User,Owner,Staff,Admin")]
        public async Task<IActionResult> NopDanhSach(int doiId)
        {
            var userId = GetUserId()!.Value;
            var doi = await _context.DoiBongs
                .Include(d => d.GiaiDau)
                .Include(d => d.ThanhViens)
                .FirstOrDefaultAsync(d => d.Id == doiId && d.DoiTruongId == userId);

            if (doi == null) return NotFound();
            if (!doi.DaThanhToan)
                return RedirectToAction("Checkout", new { doiId });

            // Đã đóng đăng ký → không cho sửa
            if (doi.GiaiDau.TrangThai == "RegistrationClosed" ||
                doi.GiaiDau.TrangThai == "Active" ||
                doi.GiaiDau.TrangThai == "Finished")
            {
                TempData["Error"] = "Danh sách đã bị khóa sau khi đóng đăng ký!";
                return RedirectToAction("Details", new { id = doi.GiaiDauId });
            }

            return View(doi);
        }

        // ══════════════════════════════════════════════════════════
        // POST /TournamentPublic/ThemThanhVien — Thêm cầu thủ
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        [YeuCauDangNhap("User,Owner,Staff,Admin")]
        public async Task<IActionResult> ThemThanhVien(
            int doiId, string hoTen, int soAo, IFormFile? anhFile)
        {
            var userId = GetUserId()!.Value;
            var doi = await _context.DoiBongs
                .Include(d => d.GiaiDau)
                .Include(d => d.ThanhViens)
                .FirstOrDefaultAsync(d => d.Id == doiId && d.DoiTruongId == userId);

            if (doi == null) return NotFound();

            // Validate khóa sổ
            if (doi.GiaiDau.TrangThai != "RegistrationOpen")
            {
                TempData["Error"] = "Danh sách đã bị khóa!";
                return RedirectToAction("NopDanhSach", new { doiId });
            }

            // Kiểm tra trùng số áo
            if (doi.ThanhViens.Any(tv => tv.SoAo == soAo))
            {
                TempData["Error"] = $"Số áo {soAo} đã có người dùng!";
                return RedirectToAction("NopDanhSach", new { doiId });
            }

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                TempData["Error"] = "Tên cầu thủ không được để trống!";
                return RedirectToAction("NopDanhSach", new { doiId });
            }

            // Xử lý upload ảnh
            string? anhDaiDien = null;
            if (anhFile != null && anhFile.Length > 0)
            {
                if (anhFile.Length > 2 * 1024 * 1024)
                {
                    TempData["Error"] = "Ảnh quá 2MB! Vui lòng chọn ảnh nhỏ hơn.";
                    return RedirectToAction("NopDanhSach", new { doiId });
                }
                var ext = Path.GetExtension(anhFile.FileName).ToLower();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!allowed.Contains(ext))
                {
                    TempData["Error"] = "Chỉ chấp nhận ảnh JPG/PNG/WebP!";
                    return RedirectToAction("NopDanhSach", new { doiId });
                }
                var folder = Path.Combine("wwwroot", "uploads", "players");
                Directory.CreateDirectory(folder);
                var fileName = $"player_{doiId}_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(folder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await anhFile.CopyToAsync(stream);
                anhDaiDien = $"/uploads/players/{fileName}";
            }

            _context.ThanhVienDois.Add(new ThanhVienDoi
            {
                DoiId = doiId,
                HoTen = hoTen.Trim(),
                SoAo = soAo,
                AnhDaiDien = anhDaiDien
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm cầu thủ {hoTen} (số {soAo})!";
            return RedirectToAction("NopDanhSach", new { doiId });
        }

        // ══════════════════════════════════════════════════════════
        // POST /TournamentPublic/XoaThanhVien — Xóa cầu thủ
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        [YeuCauDangNhap("User,Owner,Staff,Admin")]
        public async Task<IActionResult> XoaThanhVien(int thanhVienId, int doiId)
        {
            var userId = GetUserId()!.Value;
            var doi = await _context.DoiBongs
                .Include(d => d.GiaiDau)
                .FirstOrDefaultAsync(d => d.Id == doiId && d.DoiTruongId == userId);

            if (doi == null) return NotFound();
            if (doi.GiaiDau.TrangThai != "RegistrationOpen")
            {
                TempData["Error"] = "Danh sách đã bị khóa!";
                return RedirectToAction("NopDanhSach", new { doiId });
            }

            var tv = await _context.ThanhVienDois
                .FirstOrDefaultAsync(t => t.Id == thanhVienId && t.DoiId == doiId);
            if (tv != null)
            {
                _context.ThanhVienDois.Remove(tv);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa cầu thủ {tv.HoTen}!";
            }

            return RedirectToAction("NopDanhSach", new { doiId });
        }

    }
}