using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    /// <summary>
    /// SanBongContext chi con dung de doc bang Users (dropdown loc Owner) —
    /// day KHONG phai domain Giai dau nen khong chuyen sang Java.
    /// </summary>
    [YeuCauDangNhap("Admin")]
    public class AdminTournamentController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly TournamentApiService _apiService;

        public AdminTournamentController(
            SanBongContext context,
            IConfiguration config,
            TournamentApiService apiService)
        {
            _context = context;
            _config = config;
            _apiService = apiService;
        }

        private string Jwt() => _apiService.GetJwtFromContext(HttpContext) ?? "";

        // ══════════════════════════════════════════════════════════
        // GET /AdminTournament/Index
        // Toàn bộ giải đấu trên hệ thống + bộ lọc
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index(
            string? trangThai, string? keyword,
            int? ownerId, string? sapXep)
        {
            var giaiList = await _apiService.GetAllGiai(trangThai, keyword, ownerId, sapXep, Jwt());
            var kpi = await _apiService.GetKpi(Jwt());

            ViewBag.GiaiDaus = giaiList;
            ViewBag.TongGiai = kpi.TongGiai;
            ViewBag.ChoDuyet = kpi.ChoDuyet;
            ViewBag.DaDuyet = kpi.DaDuyet;
            ViewBag.DangDienRa = kpi.DangDienRa;
            ViewBag.TongLePhi = kpi.TongLePhi;

            // Users khong thuoc domain Giai dau — van doc truc tiep tu SanBongContext
            ViewBag.OwnerList = await _context.Users
                .Where(u => u.VaiTro == "Owner")
                .OrderBy(u => u.HoTen)
                .ToListAsync();

            ViewBag.FilterTrangThai = trangThai;
            ViewBag.FilterSearch = keyword;
            ViewBag.FilterOwner = ownerId?.ToString();
            ViewBag.FilterSapXep = sapXep;

            return View(giaiList);
        }

        // ══════════════════════════════════════════════════════════
        // GET /AdminTournament/Details/5 — Xem chi tiết giải
        // Admin chỉ xem, không chỉnh sửa nghiệp vụ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Details(int id)
        {
            var giai = await _apiService.GetChiTietGiaiAdmin(id, Jwt());
            if (giai == null) return NotFound();

            ViewBag.BangXepHang = await _apiService.GetBangXepHang(id);

            return View(giai);
        }

        // ══════════════════════════════════════════════════════════
        // POST /AdminTournament/PheDuyet/{id}
        // Draft → Approved — Admin phê duyệt giải trước khi Owner mở đăng ký
        // ══════════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PheDuyet(int id, string? ghiChu)
        {
            var (ok, error) = await _apiService.PheDuyet(id, ghiChu, Jwt());
            if (!ok)
            {
                TempData["Error"] = error;
                return RedirectToAction("Details", new { id });
            }

            TempData["Success"] = "✅ Đã phê duyệt giải. Owner có thể mở đăng ký.";
            return RedirectToAction("Details", new { id });
        }

        // ══════════════════════════════════════════════════════════
        // POST /AdminTournament/TuChoi/{id}
        // Draft → từ chối và xóa (Admin không duyệt)
        // ══════════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TuChoi(int id, string lyDo)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                TempData["Error"] = "Cần nhập lý do từ chối!";
                return RedirectToAction("Details", new { id });
            }

            var (ok, error) = await _apiService.TuChoi(id, lyDo, Jwt());
            if (!ok)
            {
                TempData["Error"] = error;
                return RedirectToAction("Details", new { id });
            }

            TempData["Success"] = "Đã từ chối và xóa giải.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════════
        // GET /AdminTournament/ExcelDoiSoat/5 — Admin cũng có thể tải Excel
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> ExcelDoiSoat(int id)
        {
            var bytes = await _apiService.GetExcelDoiSoatAdmin(id, Jwt());
            if (bytes == null) return NotFound();

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DoiSoat_{id}.xlsx");
        }

        // ══════════════════════════════════════════════════════════
        // GET /AdminTournament/BaoCao — Báo cáo doanh thu giải đấu
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> BaoCao()
        {
            var baoCao = await _apiService.GetBaoCao(Jwt());

            // QUAN TRONG: view nay serialize bieu6Thang sang JS bang
            // System.Text.Json.JsonSerializer.Serialize(bieu) KHONG naming
            // policy — phai giu dung ten field lowercase nhu C# goc dung
            // (anonymous object) de JS (d.thang, d.soGiai, d.doanhThu) khong vo.
            // Phai la List<object> that su (khong phai List<anonymous-type>) vi
            // View doc "ViewBag.Bieu6Thang as List<object>".
            List<object> bieu6Thang = baoCao.Bieu6Thang.Select(t => (object)new
            {
                thang = t.Thang,
                soGiai = t.SoGiai,
                soDoiThanhToan = t.SoDoiThanhToan,
                doanhThu = (double)t.DoanhThu
            }).ToList();
            ViewBag.Bieu6Thang = bieu6Thang;

            // View doc "ViewBag.TopOwner as List<dynamic>" — phai la List<dynamic>
            // (= List<object> luc runtime), khong phai List<TopOwnerRowApiDto>.
            List<dynamic> topOwner = baoCao.TopOwner
                .Select(o => (dynamic)new { o.HoTen, o.Email, o.SoGiai, o.SoActive })
                .ToList();
            ViewBag.TopOwner = topOwner;

            ViewBag.TongGiaiDau = baoCao.TongGiaiDau;
            ViewBag.GiaiHoanThanh = baoCao.GiaiHoanThanh;
            ViewBag.TongDoi = baoCao.TongDoi;
            ViewBag.TongTranDau = baoCao.TongTranDau;
            ViewBag.TongDoanhThu = baoCao.TongDoanhThu;

            return View();
        }
    }
}
