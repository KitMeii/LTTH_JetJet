using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    /// <summary>
    /// Tournament Controller — chỉ làm 3 việc:
    /// 1. Nhận request HTTP
    /// 2. Gọi TournamentApiService (goi sang tournament-service Java qua HttpClient)
    /// 3. Trả về View hoặc redirect
    /// KHÔNG chứa logic nghiệp vụ — logic nam ben Java (TournamentService.java).
    ///
    /// SanBongContext chi con dung de doc bang SanBongs (chon san khi tao giai) —
    /// day KHONG phai domain Giai dau nen khong chuyen sang Java, van doc truc tiep.
    /// </summary>
    [YeuCauDangNhap]
    public class TournamentController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly TournamentApiService _apiService;

        public TournamentController(
            SanBongContext context,
            IConfiguration config,
            TournamentApiService apiService)
        {
            _context = context;
            _config = config;
            _apiService = apiService;
        }

        private int OwnerId() => TokenHelper.LayUserId(Request, _config)!.Value;
        private string Jwt() => _apiService.GetJwtFromContext(HttpContext) ?? "";

        // ── GET /Tournament ──────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var list = await _apiService.GetMyTournaments(Jwt());
            return View(list);
        }

        // ── GET /Tournament/Create ───────────────────────────────
        public async Task<IActionResult> Create()
        {
            ViewBag.SanList = await LaySanCuaOwner();
            return View();
        }

        // ── POST /Tournament/Create ──────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGiaiDauDto dto)
        {
            var (ok, error, giai) = await _apiService.TaoGiai(dto, Jwt());
            if (!ok || giai == null)
            {
                TempData["Error"] = error ?? "Tạo giải thất bại!";
                ViewBag.SanList = await LaySanCuaOwner();
                return View(dto);
            }

            TempData["Success"] = $"Tạo giải '{giai.TenGiai}' thành công!";
            return RedirectToAction("Details", new { id = giai.Id });
        }

        // ── GET /Tournament/Details/5 ────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var giai = await _apiService.GetChiTietGiaiOwner(id, Jwt());
            if (giai == null) return NotFound();

            ViewBag.BangXepHang = await _apiService.GetBangXepHang(id);
            return View(giai);
        }

        // ── POST /Tournament/MoiDangKy ───────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MoiDangKy(int id)
        {
            var (ok, error) = await _apiService.MoiDangKy(id, Jwt());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("Details", new { id }); }

            TempData["Success"] = "Đã mở đăng ký!";
            return RedirectToAction("Details", new { id });
        }

        // ── POST /Tournament/DongDangKy ──────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DongDangKy(int id)
        {
            var (ok, error) = await _apiService.DongDangKy(id, Jwt());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("Details", new { id }); }

            TempData["Success"] = "Đã đóng đăng ký! Tiến hành chia bảng.";
            return RedirectToAction("ChiaBang", new { id });
        }

        // ── GET /Tournament/ChiaBang/5 ───────────────────────────
        public async Task<IActionResult> ChiaBang(int id)
        {
            var giai = await _apiService.GetChiTietGiaiOwner(id, Jwt());
            if (giai == null) return NotFound();
            if (giai.TrangThai != "RegistrationClosed")
            {
                TempData["Error"] = "Chỉ chia bảng sau khi đóng đăng ký!";
                return RedirectToAction("Details", new { id });
            }
            return View(giai);
        }

        // ── POST /Tournament/GanBang (AJAX) ──────────────────────
        [HttpPost]
        public async Task<IActionResult> GanBang(int doiId, int? bangId)
        {
            var (ok, error) = await _apiService.GanBang(doiId, bangId, Jwt());
            if (!ok) return Json(new { ok = false, message = error });

            return Json(new { ok = true });
        }

        // ── POST /Tournament/KhoiTao ─────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KhoiTao(int id)
        {
            var (ok, error) = await _apiService.KhoiTao(id, Jwt());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("ChiaBang", new { id }); }

            TempData["Success"] = "Khởi tạo thành công! Email lịch đấu đã gửi cho các đội.";
            return RedirectToAction("Details", new { id });
        }

        // ── POST /Tournament/KetThuc ─────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KetThuc(int id)
        {
            var (ok, error) = await _apiService.KetThuc(id, Jwt());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("Details", new { id }); }

            TempData["Success"] = "Giải kết thúc! Tải Excel đối soát bên dưới.";
            return RedirectToAction("Details", new { id });
        }

        // ── POST /Tournament/XuLySuCo ────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLySuCo(int tranDauId, int doiBoCuocId, string lyDo)
        {
            var (ok, error) = await _apiService.XuLySuCo(tranDauId, doiBoCuocId, lyDo, Jwt());
            if (!ok) TempData["Error"] = error;
            else TempData["Success"] = "Đã xử lý sự cố. Đội vi phạm thua 0-3.";

            var tran = await _apiService.GetChiTietTran(tranDauId, Jwt());
            return RedirectToAction("Details", new { id = tran?.GiaiDauId });
        }

        // ── GET /Tournament/ExcelDoiSoat/5 ──────────────────────
        public async Task<IActionResult> ExcelDoiSoat(int id)
        {
            var bytes = await _apiService.GetExcelDoiSoat(id, Jwt());
            if (bytes == null) return NotFound();

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DoiSoat_{id}.xlsx");
        }

        // ══════════════════════════════════════════════════════════
        // ⭐ THEM MOI — tinh nang hoan thien (xac nhan thanh toan doi)
        // Chua co UI/View rieng (khong duoc sua .cshtml) — expose duoi
        // dang JSON de FE goi AJAX khi can, hoac test truc tiep qua Postman.
        // ══════════════════════════════════════════════════════════

        // GET /Tournament/DanhSachDoi/5 — danh sach doi kem trang thai thanh toan
        [HttpGet]
        public async Task<IActionResult> DanhSachDoi(int id)
        {
            var doiList = await _apiService.GetDanhSachDoi(id, Jwt());
            return Json(doiList.Select(d => new
            {
                d.Id,
                d.TenDoi,
                d.DoiTruongId,
                DoiTruongHoTen = d.DoiTruong?.HoTen,
                d.DaThanhToan,
                d.ThoiGianThanhToan,
                d.TienKyQuyConLai
            }));
        }

        // POST /Tournament/XacNhanThanhToan — Owner xac nhan da nhan tien tu doi
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanThanhToan(int giaiId, int doiId)
        {
            var (ok, error) = await _apiService.XacNhanThanhToan(giaiId, doiId, Jwt());
            if (!ok) return Json(new { ok = false, message = error });

            return Json(new { ok = true, message = "Đã xác nhận thanh toán!" });
        }

        // ══════════════════════════════════════════════════════════
        // KNOCK-OUT — noi day Views/Tournament/Bracket.cshtml (co san) vao Java
        // ══════════════════════════════════════════════════════════

        // GET /Tournament/Bracket/5 — cay bracket knock-out
        // View da co san, doc Model.TranDaus loc theo LoaiVong + Model.DoiBongs
        // (tim vo dich) — nen dung lai GetChiTietGiaiOwner() (giong Details()),
        // KHONG dung GetBracket() vi Java /bracket khong tra kem DoiBongs.
        public async Task<IActionResult> Bracket(int id)
        {
            var giai = await _apiService.GetChiTietGiaiOwner(id, Jwt());
            if (giai == null) return NotFound();

            ViewBag.IsOwner = true;
            ViewBag.KhungGios = await _context.KhungGios
                .Where(k => k.SanBongId == giai.SanBongId)
                .ToListAsync();

            return View(giai);
        }

        // POST /Tournament/SinhBracket — trung ten/tham so voi form co san trong
        // Bracket.cshtml (action="/Tournament/SinhBracket", hidden field "giaiDauId")
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SinhBracket(int giaiDauId)
        {
            var (ok, error) = await _apiService.SinhVongKnockOut(giaiDauId, Jwt());
            if (!ok) TempData["Error"] = error ?? "Không sinh được vòng knock-out!";
            else TempData["Success"] = "🏆 Đã sinh vòng knock-out!";

            return RedirectToAction("Bracket", new { id = giaiDauId });
        }

        // ── Helper ─────────────────────────────────────────────
        private async Task<List<SanBong>> LaySanCuaOwner()
        {
            return await _context.SanBongs
                .Where(s => s.OwnerId == OwnerId()
                         && s.TrangThaiDuyet == "DaDuyet"
                         && !s.IsHidden)
                .ToListAsync();
        }
    }
}
