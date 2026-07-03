using Microsoft.AspNetCore.Mvc;
using Web_Stadium.Filters;
using Web_Stadium.Services;
using Web_Stadium.Services.Dto.Go;

namespace Web_Stadium.Controllers
{
    /// <summary>
    /// Da bo phan SignalR broadcast (TournamentHub) theo yeu cau — chi con
    /// REST qua TournamentApiService, khong con _context/DbContext truc tiep.
    /// </summary>
    [YeuCauDangNhap("Staff")]
    public class TournamentStaffController : Controller
    {
        private readonly IConfiguration _config;
        private readonly TournamentApiService _apiService;
        private readonly GoStaffApiClient _goClient;

        public TournamentStaffController(
            IConfiguration config,
            TournamentApiService apiService,
            GoStaffApiClient goClient)
        {
            _config = config;
            _apiService = apiService;
            _goClient = goClient;
        }

        private string Jwt() => _apiService.GetJwtFromContext(HttpContext) ?? "";

        // ══════════════════════════════════════════════════════════
        // GET /TournamentStaff/TranDau/{id} — Chi tiết trận đấu
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> TranDau(int id)
        {
            var tran = await _apiService.GetChiTietTran(id, Jwt());
            if (tran == null) return NotFound();
            return View(tran);
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentStaff/DanhSach — Danh sách trận phân công
        // Filter: hôm nay / tuần này / tất cả + trạng thái
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> DanhSach(string? loc, string? trangThai)
        {
            var tranList = await _apiService.GetTranDauHomNay(loc, trangThai, Jwt());

            ViewBag.Loc = loc ?? "hom_nay";
            ViewBag.TrangThai = trangThai;

            var homNay = DateTime.Today;
            var tatCaHomNay = (await _apiService.GetTranDauHomNay("hom_nay", null, Jwt()));
            ViewBag.SoTranHomNay = tatCaHomNay.Count;
            ViewBag.SoTranChuaBatDau = tatCaHomNay.Count(t => t.TrangThai == "Scheduled");

            return View(tranList);
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentStaff/CheckIn/{tranDauId}
        // Xem danh sách cầu thủ 2 đội — check-in từng người
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> CheckIn(int id)
        {
            var (ok, error, tran) = await _apiService.CheckIn(id, Jwt());
            if (!ok || tran == null)
            {
                if (error != null)
                {
                    TempData["Error"] = error;
                    return RedirectToAction("DanhSach");
                }
                return NotFound();
            }

            if (tran.TrangThai == "Closed")
            {
                TempData["Error"] = "Trận này đã kết thúc!";
                return RedirectToAction("DanhSach");
            }

            ViewBag.TranId = id;
            return View(tran);
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentStaff/SuKien/{tranDauId}
        // Ghi sự kiện realtime: bàn thắng, thẻ vàng, thẻ đỏ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> SuKien(int id)
        {
            var tran = await _apiService.GetChiTietTran(id, Jwt());
            if (tran == null) return NotFound();
            if (tran.TrangThai != "InProgress")
            {
                TempData["Error"] = "Trận chưa bắt đầu hoặc đã kết thúc!";
                return RedirectToAction("DanhSach");
            }

            ViewBag.TysoNha = tran.SuKiens.Count(s => s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiNhaId);
            ViewBag.TysoKhach = tran.SuKiens.Count(s => s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiKhachId);

            return View(tran);
        }

        // ══════════════════════════════════════════════════════════
        // POST /TournamentStaff/GhiSuKien — AJAX ghi sự kiện
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> GhiSuKien(
            int tranDauId, int? thanhVienId, int doiId,
            string loaiSuKien, int phut, string? ghiChu)
        {
            var (ok, error, loaiThucTe, tysoNha, tysoKhach) = await _apiService.GhiSuKien(
                tranDauId, thanhVienId, doiId, loaiSuKien, phut, ghiChu, Jwt());

            if (!ok)
                return Json(new { ok = false, message = error ?? "Không ghi được sự kiện!" });

            return Json(new
            {
                ok = true,
                loaiThucTe,
                tysoNha,
                tysoKhach,
                message = loaiThucTe == "TheVangLan2"
                    ? "⚠️ Thẻ vàng lần 2 — tự động thẻ đỏ!"
                    : "Đã ghi sự kiện"
            });
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentStaff/KetThuc/{tranDauId}
        // Soft Lock Summary — đọc kết quả cho 2 đội trưởng xác nhận
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> KetThuc(int id)
        {
            var tran = await _apiService.GetChiTietTran(id, Jwt());
            if (tran == null) return NotFound();
            if (tran.TrangThai != "InProgress")
            {
                TempData["Error"] = "Trận không đang diễn ra!";
                return RedirectToAction("DanhSach");
            }

            ViewBag.TysoNha = tran.SuKiens.Count(s => s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiNhaId);
            ViewBag.TysoKhach = tran.SuKiens.Count(s => s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiKhachId);

            return View(tran);
        }

        // ══════════════════════════════════════════════════════════
        // POST /TournamentStaff/XacNhanKetThuc — Chốt kết quả trận
        // ══════════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanKetThuc(int tranDauId)
        {
            var (ok, error, banThangNha, banThangKhach) = await _apiService.XacNhanKetThuc(tranDauId, Jwt());
            if (!ok)
            {
                TempData["Error"] = error ?? "Trận không đang diễn ra!";
                return RedirectToAction("DanhSach");
            }

            // Broadcast realtime (SignalR) da bo theo yeu cau — client tu polling neu can.
            TempData["Success"] = $"✅ Đã chốt trận! Kết quả: {banThangNha} – {banThangKhach}";
            return RedirectToAction("DanhSach");
        }

        // ══════════════════════════════════════════════════════════
        // POST /TournamentStaff/HuyBanThang — Xóa bàn thắng nhầm
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> HuyBanThang(int suKienId, int tranDauId)
        {
            var (tysoNha, tysoKhach) = await _apiService.HuyBanThang(tranDauId, suKienId, Jwt());
            return Json(new { ok = true, tysoNha, tysoKhach });
        }

        // ══════════════════════════════════════════════════════════
        // ⭐ GoStaffApi (port 8082) — điểm danh cầu thủ giải đấu (tính năng MỚI,
        // tách biệt hoàn toàn với check-in đơn đặt sân đang có ở trên/StaffController).
        // ══════════════════════════════════════════════════════════

        // GET /TournamentStaff/DiemDanhCauThu?matchId=... — danh sách cầu thủ 2 đội
        // kèm trạng thái đã điểm danh chưa. Go chỉ trả ID đã điểm danh (không có
        // tên) nên phải ghép với roster thật lấy từ tournament-service qua
        // TournamentApiService.GetChiTietTran() (KHÔNG sửa tournament-service).
        [HttpGet]
        public async Task<IActionResult> DiemDanhCauThu(int matchId)
        {
            var tran = await _apiService.GetChiTietTran(matchId, Jwt());
            if (tran == null) return Json(new { ok = false, message = "Không tìm thấy trận đấu!" });

            var (ok, message, goData) = await _goClient.GetCheckIns(matchId, Jwt());
            var checkedIds = new HashSet<int>(goData?.CheckedPlayerIds ?? new List<int>());

            var roster = new List<PlayerCheckInDto>();
            foreach (var tv in tran.DoiNha?.ThanhViens ?? new List<Web_Stadium.EFCore.ThanhVienDoi>())
            {
                roster.Add(new PlayerCheckInDto
                {
                    ThanhVienDoiId = tv.Id,
                    TenCauThu = tv.HoTen,
                    SoAo = tv.SoAo,
                    Doi = "nha",
                    IsCheckedIn = checkedIds.Contains(tv.Id)
                });
            }
            foreach (var tv in tran.DoiKhach?.ThanhViens ?? new List<Web_Stadium.EFCore.ThanhVienDoi>())
            {
                roster.Add(new PlayerCheckInDto
                {
                    ThanhVienDoiId = tv.Id,
                    TenCauThu = tv.HoTen,
                    SoAo = tv.SoAo,
                    Doi = "khach",
                    IsCheckedIn = checkedIds.Contains(tv.Id)
                });
            }

            return Json(new
            {
                ok,
                message = ok ? null : message,
                matchId,
                tenDoiNha = tran.DoiNha?.TenDoi,
                tenDoiKhach = tran.DoiKhach?.TenDoi,
                totalChecked = goData?.TotalChecked ?? 0,
                roster
            });
        }

        // POST /TournamentStaff/ToggleDiemDanh — bật/tắt điểm danh 1 cầu thủ (AJAX)
        [HttpPost]
        public async Task<IActionResult> ToggleDiemDanh(int matchId, int playerId)
        {
            var (ok, message, data) = await _goClient.TogglePlayerCheckIn(matchId, playerId, Jwt());
            if (!ok || data == null)
                return Json(new { ok = false, message = message ?? "Không điểm danh được!" });

            return Json(new
            {
                ok = true,
                playerId = data.PlayerId,
                @checked = data.Checked,
                totalChecked = data.TotalChecked,
                message = data.Message
            });
        }
    }
}
