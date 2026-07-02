using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Staff")]
    public class TournamentStaffController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly SuspensionService _suspensionService;
        private readonly StandingService _standingService;
        private readonly TournamentNotificationService _notifService;

        public TournamentStaffController(
            SanBongContext context,
            IConfiguration config,
            SuspensionService suspensionService,
            StandingService standingService,
            TournamentNotificationService notifService)
        {
            _context = context;
            _config = config;
            _suspensionService = suspensionService;
            _standingService = standingService;
            _notifService = notifService;
        }

        private int StaffId() => TokenHelper.LayUserId(Request, _config)!.Value;

        // Lấy sân Staff được phân công
        private async Task<List<int>> SanDuocGiao()
            => await _context.StaffSanPhanCongs
                .Where(p => p.StaffId == StaffId())
                .Select(p => p.SanBongId)
                .ToListAsync();

        // ══════════════════════════════════════════════════════════
        // GET /TournamentStaff/DanhSach — Danh sách trận phân công
        // Filter: hôm nay / tuần này / tất cả + trạng thái
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> DanhSach(string? loc, string? trangThai)
        {
            var sanIds = await SanDuocGiao();

            // Tính trước các mốc thời gian — tránh EFCore không dịch được
            var homNay = DateTime.Today;
            var dauTuan = homNay.AddDays(-(int)homNay.DayOfWeek);
            var cuoiTuan = dauTuan.AddDays(7);

            var query = _context.TranDaus
                .Include(t => t.GiaiDau).ThenInclude(g => g.SanBong)
                .Include(t => t.DoiNha)
                .Include(t => t.DoiKhach)
                .Where(t => sanIds.Contains(t.GiaiDau.SanBongId)
                         && t.GiaiDau.TrangThai == "Active");

            // Lọc thời gian — dùng biến đã tính sẵn
            query = loc switch
            {
                "hom_nay" => query.Where(t => t.NgayThiDau >= homNay
                                            && t.NgayThiDau < homNay.AddDays(1)),
                "tuan_nay" => query.Where(t => t.NgayThiDau >= dauTuan
                                            && t.NgayThiDau < cuoiTuan),
                _ => query
            };

            // Lọc trạng thái
            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(t => t.TrangThai == trangThai);

            var tranList = await query
                .OrderBy(t => t.NgayThiDau)
                .ThenBy(t => t.TrangThai)
                .ToListAsync();

            ViewBag.Loc = loc ?? "hom_nay";
            ViewBag.TrangThai = trangThai;

            // KPI cho dashboard
            ViewBag.SoTranHomNay = await _context.TranDaus
                .CountAsync(t => sanIds.Contains(t.GiaiDau.SanBongId)
                              && t.NgayThiDau.Date == DateTime.Today
                              && t.GiaiDau.TrangThai == "Active");
            ViewBag.SoTranChuaBatDau = await _context.TranDaus
                .CountAsync(t => sanIds.Contains(t.GiaiDau.SanBongId)
                              && t.NgayThiDau.Date == DateTime.Today
                              && t.TrangThai == "Scheduled"
                              && t.GiaiDau.TrangThai == "Active");

            return View(tranList);
        }

        // ══════════════════════════════════════════════════════════
        // GET /TournamentStaff/CheckIn/{tranDauId}
        // Xem danh sách cầu thủ 2 đội — check-in từng người
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> CheckIn(int id)
        {
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau).ThenInclude(g => g.SanBong)
                .Include(t => t.DoiNha).ThenInclude(d => d.ThanhViens)
                .Include(t => t.DoiKhach).ThenInclude(d => d.ThanhViens)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tran == null) return NotFound();

            // Validate Staff có được phân công sân này không
            var sanIds = await SanDuocGiao();
            if (!sanIds.Contains(tran.GiaiDau.SanBongId))
                return Forbid();

            if (tran.TrangThai == "Closed")
            {
                TempData["Error"] = "Trận này đã kết thúc!";
                return RedirectToAction("DanhSach");
            }

            // Gán Staff phụ trách nếu chưa có
            if (tran.StaffPhuTrachId == null)
            {
                tran.StaffPhuTrachId = StaffId();
                await _context.SaveChangesAsync();
            }

            // Đánh dấu InProgress nếu chưa
            if (tran.TrangThai == "Scheduled")
            {
                tran.TrangThai = "InProgress";
                await _context.SaveChangesAsync();
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
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau)
                .Include(t => t.DoiNha).ThenInclude(d => d.ThanhViens)
                .Include(t => t.DoiKhach).ThenInclude(d => d.ThanhViens)
                .Include(t => t.SuKiens).ThenInclude(s => s.ThanhVien)
                .Include(t => t.SuKiens).ThenInclude(s => s.Doi)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tran == null) return NotFound();
            if (tran.TrangThai != "InProgress")
            {
                TempData["Error"] = "Trận chưa bắt đầu hoặc đã kết thúc!";
                return RedirectToAction("DanhSach");
            }

            // Tính tỷ số hiện tại từ SuKienTran
            ViewBag.TysoNha = tran.SuKiens.Count(s =>
                s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiNhaId);
            ViewBag.TysoKhach = tran.SuKiens.Count(s =>
                s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiKhachId);

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
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau)
                .Include(t => t.SuKiens)
                .FirstOrDefaultAsync(t => t.Id == tranDauId);

            if (tran == null)
                return Json(new { ok = false, message = "Không tìm thấy trận!" });
            if (tran.TrangThai != "InProgress")
                return Json(new { ok = false, message = "Trận không đang diễn ra!" });

            // Kiểm tra cầu thủ bị treo giò
            if (thanhVienId.HasValue && loaiSuKien is "BanThang" or "TheVang" or "TheDo")
            {
                var tv = await _context.ThanhVienDois.FindAsync(thanhVienId.Value);
                if (tv != null && tv.SoTranTreoGio > 0 && loaiSuKien != "BanThang")
                    return Json(new { ok = false, message = $"{tv.HoTen} đang bị treo giò!" });
            }

            // Kiểm tra thẻ vàng lần 2 → tự động thêm thẻ đỏ
            string loaiThucTe = loaiSuKien;
            if (loaiSuKien == "TheVang" && thanhVienId.HasValue)
            {
                var soVangTranNay = tran.SuKiens.Count(s =>
                    s.ThanhVienId == thanhVienId && s.LoaiSuKien == "TheVang");
                if (soVangTranNay >= 1)
                    loaiThucTe = "TheVangLan2"; // sẽ xử lý như thẻ đỏ
            }

            var sk = new SuKienTran
            {
                TranDauId = tranDauId,
                ThanhVienId = thanhVienId,
                DoiId = doiId,
                LoaiSuKien = loaiThucTe,
                Phut = phut,
                GhiChu = ghiChu?.Trim(),
                ThoiGianGhi = DateTime.Now
            };
            _context.SuKienTrans.Add(sk);

            // Cập nhật thống kê cầu thủ ngay
            if (thanhVienId.HasValue)
            {
                var tv = await _context.ThanhVienDois.FindAsync(thanhVienId.Value);
                if (tv != null)
                {
                    if (loaiThucTe == "BanThang") tv.TongBanThang++;
                    if (loaiThucTe == "TheVang") tv.TongTheVang++;
                    if (loaiThucTe is "TheDo" or "TheVangLan2") tv.TongTheDo++;
                }
            }

            await _context.SaveChangesAsync();

            // Tính lại tỷ số
            var tysoNha = await _context.SuKienTrans.CountAsync(s =>
                s.TranDauId == tranDauId && s.LoaiSuKien == "BanThang"
                && s.DoiId == tran.DoiNhaId);
            var tysoKhach = await _context.SuKienTrans.CountAsync(s =>
                s.TranDauId == tranDauId && s.LoaiSuKien == "BanThang"
                && s.DoiId == tran.DoiKhachId);

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
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau)
                .Include(t => t.DoiNha)
                .Include(t => t.DoiKhach)
                .Include(t => t.SuKiens).ThenInclude(s => s.ThanhVien)
                .Include(t => t.SuKiens).ThenInclude(s => s.Doi)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tran == null) return NotFound();
            if (tran.TrangThai != "InProgress")
            {
                TempData["Error"] = "Trận không đang diễn ra!";
                return RedirectToAction("DanhSach");
            }

            // Tính tỷ số từ SuKienTran
            ViewBag.TysoNha = tran.SuKiens.Count(s =>
                s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiNhaId);
            ViewBag.TysoKhach = tran.SuKiens.Count(s =>
                s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiKhachId);

            return View(tran);
        }

        // ══════════════════════════════════════════════════════════
        // POST /TournamentStaff/XacNhanKetThuc — Chốt kết quả trận
        // ══════════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanKetThuc(int tranDauId)
        {
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau)
                .Include(t => t.SuKiens)
                .FirstOrDefaultAsync(t => t.Id == tranDauId);

            if (tran == null) return NotFound();
            if (tran.TrangThai != "InProgress")
            {
                TempData["Error"] = "Trận không đang diễn ra!";
                return RedirectToAction("DanhSach");
            }

            // Tính tỷ số từ SuKienTran
            tran.BanThangNha = tran.SuKiens
                .Count(s => s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiNhaId);
            tran.BanThangKhach = tran.SuKiens
                .Count(s => s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiKhachId);
            tran.TrangThai = "Closed";

            await _context.SaveChangesAsync();

            // Tự động xử lý treo giò sau trận
            await _suspensionService.XuLyTreoGio(tran.GiaiDauId);

            // Email kết quả đã bỏ theo flow thực tế

            TempData["Success"] =
                $"✅ Đã chốt trận! Kết quả: {tran.BanThangNha} – {tran.BanThangKhach}";
            return RedirectToAction("DanhSach");
        }

        // ══════════════════════════════════════════════════════════
        // POST /TournamentStaff/HuyBanThang — Xóa bàn thắng nhầm
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> HuyBanThang(int suKienId, int tranDauId)
        {
            var sk = await _context.SuKienTrans
                .Include(s => s.ThanhVien)
                .FirstOrDefaultAsync(s => s.Id == suKienId);

            if (sk != null)
            {
                // Trừ lại thống kê cầu thủ
                if (sk.ThanhVien != null && sk.LoaiSuKien == "BanThang")
                    sk.ThanhVien.TongBanThang = Math.Max(0, sk.ThanhVien.TongBanThang - 1);

                _context.SuKienTrans.Remove(sk);
                await _context.SaveChangesAsync();
            }

            // Tính lại tỷ số
            var tran = await _context.TranDaus
                .Include(t => t.SuKiens)
                .FirstOrDefaultAsync(t => t.Id == tranDauId);

            var tysoNha = tran?.SuKiens.Count(s =>
                s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiNhaId) ?? 0;
            var tysoKhach = tran?.SuKiens.Count(s =>
                s.LoaiSuKien == "BanThang" && s.DoiId == tran.DoiKhachId) ?? 0;

            return Json(new { ok = true, tysoNha, tysoKhach });
        }
    }
}