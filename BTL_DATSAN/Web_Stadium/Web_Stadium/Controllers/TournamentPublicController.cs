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
        private readonly TournamentApiService _apiService;
        private readonly CloudinaryService _cloudinaryService;

        public TournamentPublicController(
            SanBongContext context,
            IConfiguration config,
            TournamentApiService apiService,
            CloudinaryService cloudinaryService)
        {
            _context = context;
            _config = config;
            _apiService = apiService;
            _cloudinaryService = cloudinaryService;
        }

        private int? GetUserId() => TokenHelper.LayUserId(Request, _config);
        private string? GetJwt() => _apiService.GetJwtFromContext(HttpContext);

        // ══════════════════════════════════════════════════════════
        // GET /TournamentPublic — Tìm kiếm giải đấu công khai
        // Bộ lọc: tên, sân, quận, trạng thái, lệ phí
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index(
            string? keyword, string? quan, string? trangThai,
            decimal? lePhiTu, decimal? lePhiDen, string? sapXep)
        {
            var giaiList = await _apiService.GetDanhSachGiai(keyword, quan, trangThai, lePhiTu, lePhiDen, sapXep);

            // DanhMucQuan khong thuoc domain Giai dau — van doc truc tiep tu SanBongContext
            ViewBag.DanhSachQuan = await _context.DanhMucQuans
                .Where(q => q.IsActive).OrderBy(q => q.ThuTu).ToListAsync();

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
            var giaiDau = await _apiService.GetChiTietGiai(id);
            if (giaiDau == null) return NotFound();

            var userId = GetUserId();

            ViewBag.DoiCuaToi = userId.HasValue
                ? giaiDau.DoiBongs.FirstOrDefault(d => d.DoiTruongId == userId.Value)
                : null;

            ViewBag.BangXepHang = await _apiService.GetBangXepHang(id);

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
            var giaiDau = await _apiService.GetChiTietGiai(id);
            if (giaiDau == null) return NotFound();

            if (giaiDau.TrangThai != "RegistrationOpen")
            {
                TempData["Error"] = "Giải đấu không trong thời gian đăng ký!";
                return RedirectToAction("Details", new { id });
            }

            var userId = GetUserId()!.Value;
            var daDangKy = giaiDau.DoiBongs.Any(d => d.DoiTruongId == userId);
            if (daDangKy)
            {
                TempData["Error"] = "Bạn đã đăng ký giải này rồi!";
                return RedirectToAction("Details", new { id });
            }

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
            if (string.IsNullOrWhiteSpace(tenDoi))
            {
                TempData["Error"] = "Tên đội không được để trống!";
                return RedirectToAction("DangKy", new { id = giaiDauId });
            }

            var (ok, error, doi) = await _apiService.DangKyDoi(giaiDauId, tenDoi.Trim(), GetJwt()!);
            if (!ok || doi == null)
            {
                TempData["Error"] = error ?? "Đăng ký thất bại!";
                return RedirectToAction("DangKy", new { id = giaiDauId });
            }

            // Redirect sang trang checkout 15 phút
            return RedirectToAction("Checkout", new { doiId = doi.Id });
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentPublic/Checkout/{doiId} — Thanh toán 15 phút
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap("User,Owner,Staff,Admin")]
        public async Task<IActionResult> Checkout(int doiId)
        {
            var doi = await _apiService.GetDoiCuaToi(doiId, GetJwt()!);
            if (doi == null) return NotFound();
            if (doi.DaThanhToan)
                return RedirectToAction("NopDanhSach", new { doiId });

            // Tinh thoi gian con lai (15 phut tu khi tao doi)
            var hetHan = doi.ThoiGianTao.AddMinutes(15);
            var conLaiGiay = Math.Max(0, (int)(hetHan - DateTime.Now).TotalSeconds);

            ViewBag.ConLaiGiay = conLaiGiay;
            ViewBag.TongThanhToan = doi.GiaiDau.LePhiGiai + doi.GiaiDau.TienKyQuy;
            return View(doi);
        }

        // XacNhanThanhToan đã được chuyển sang Owner (xem TournamentController.XacNhanThanhToan)

        // ══════════════════════════════════════════════════════════
        // GET /TournamentPublic/NopDanhSach/{doiId} — Nhập thành viên
        // ══════════════════════════════════════════════════════════
        [YeuCauDangNhap("User,Owner,Staff,Admin")]
        public async Task<IActionResult> NopDanhSach(int doiId)
        {
            var doi = await _apiService.GetDoiCuaToi(doiId, GetJwt()!);
            if (doi == null) return NotFound();
            if (!doi.DaThanhToan)
                return RedirectToAction("Checkout", new { doiId });

            if (doi.GiaiDau.TrangThai is "RegistrationClosed" or "Active" or "Finished")
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
            if (string.IsNullOrWhiteSpace(hoTen))
            {
                TempData["Error"] = "Tên cầu thủ không được để trống!";
                return RedirectToAction("NopDanhSach", new { doiId });
            }

            // Xu ly upload anh (CloudinaryService van thuoc C#, khong thuoc domain giai dau)
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
                anhDaiDien = await _cloudinaryService.UploadAnhAsync(anhFile, "players");
            }

            var (ok, error) = await _apiService.ThemThanhVien(doiId, hoTen.Trim(), soAo, anhDaiDien, GetJwt()!);
            if (!ok)
            {
                TempData["Error"] = error ?? "Không thêm được cầu thủ!";
                return RedirectToAction("NopDanhSach", new { doiId });
            }

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
            var jwt = GetJwt()!;

            // Lay anh dai dien truoc khi xoa de don rac Cloudinary
            var roster = await _apiService.GetRoster(doiId, jwt);
            var tv = roster.FirstOrDefault(t => t.Id == thanhVienId);

            var (ok, error) = await _apiService.XoaThanhVien(doiId, thanhVienId, jwt);
            if (!ok)
            {
                TempData["Error"] = error ?? "Không xóa được cầu thủ!";
                return RedirectToAction("NopDanhSach", new { doiId });
            }

            if (tv != null)
            {
                if (!string.IsNullOrEmpty(tv.AnhDaiDien))
                {
                    var publicId = _cloudinaryService.LayPublicId(tv.AnhDaiDien);
                    if (publicId != null)
                        _ = Task.Run(() => _cloudinaryService.XoaAnhAsync(publicId));
                }
                TempData["Success"] = $"Đã xóa cầu thủ {tv.HoTen}!";
            }

            return RedirectToAction("NopDanhSach", new { doiId });
        }
    }
}
