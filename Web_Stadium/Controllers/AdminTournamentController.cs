using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Admin")]
    public class AdminTournamentController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly StandingService _standingService;
        private readonly TournamentExcelService _excelService;

        public AdminTournamentController(
            SanBongContext context,
            IConfiguration config,
            StandingService standingService,
            TournamentExcelService excelService)
        {
            _context = context;
            _config = config;
            _standingService = standingService;
            _excelService = excelService;
        }

        private int AdminId() => TokenHelper.LayUserId(Request, _config)!.Value;

        private async Task GhiLog(string hanhDong, string doiTuong, int id, string moTa)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = AdminId(),
                VaiTro = "Admin",
                HanhDong = hanhDong,
                DoiTuong = doiTuong,
                DoiTuongId = id,
                MoTa = moTa,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // GET /AdminTournament/Index
        // Toàn bộ giải đấu trên hệ thống + bộ lọc
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index(
            string? trangThai, string? keyword,
            int? ownerId, string? sapXep)
        {
            var query = _context.GiaiDaus
                .Include(g => g.SanBong)
                .Include(g => g.Owner)
                .Include(g => g.DoiBongs)
                .Include(g => g.TranDaus)
                .AsQueryable();

            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(g => g.TrangThai == trangThai);

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(g =>
                    g.TenGiai.Contains(keyword) ||
                    g.SanBong.TenSan.Contains(keyword) ||
                    g.Owner.HoTen.Contains(keyword));

            if (ownerId.HasValue)
                query = query.Where(g => g.OwnerId == ownerId.Value);

            query = sapXep switch
            {
                "moi_nhat" => query.OrderByDescending(g => g.ThoiGianTao),
                "cu_nhat" => query.OrderBy(g => g.ThoiGianTao),
                "ten" => query.OrderBy(g => g.TenGiai),
                _ => query.OrderByDescending(g => g.ThoiGianTao)
            };

            var giaiList = await query.ToListAsync();

            // KPI tổng hợp
            ViewBag.TongGiai = await _context.GiaiDaus.CountAsync();
            ViewBag.GiaiChoDuyet = await _context.GiaiDaus.CountAsync(g => g.TrangThai == "Draft");
            ViewBag.GiaiDangDienRa = await _context.GiaiDaus.CountAsync(g => g.TrangThai == "Active");
            ViewBag.GiaiDangDangKy = await _context.GiaiDaus.CountAsync(g => g.TrangThai == "RegistrationOpen");
            ViewBag.TongDoanhThuLePhi = await _context.DoiBongs
                .Where(d => d.DaThanhToan)
                .SumAsync(d => d.GiaiDau.LePhiGiai);

            // Danh sách Owner cho dropdown lọc
            ViewBag.OwnerList = await _context.Users
                .Where(u => u.VaiTro == "Owner")
                .OrderBy(u => u.HoTen)
                .ToListAsync();

            ViewBag.TrangThai = trangThai;
            ViewBag.Keyword = keyword;
            ViewBag.OwnerId = ownerId;
            ViewBag.SapXep = sapXep;

            return View(giaiList);
        }

        // ══════════════════════════════════════════════════════════
        // GET /AdminTournament/Details/5 — Xem chi tiết giải
        // Admin chỉ xem, không chỉnh sửa nghiệp vụ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Details(int id)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.SanBong)
                .Include(g => g.Owner)
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs).ThenInclude(d => d.ThanhViens)
                .Include(g => g.DoiBongs).ThenInclude(d => d.Bang)
                .Include(g => g.DoiBongs).ThenInclude(d => d.DoiTruong)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiNha)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiKhach)
                .Include(g => g.TranDaus).ThenInclude(t => t.SuKiens)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (giai == null) return NotFound();

            ViewBag.BangXepHang = await _standingService.GetStandings(id);

            return View(giai);
        }

        // ══════════════════════════════════════════════════════════
        // POST /AdminTournament/PheDuyet/{id}
        // Draft → Approved — Admin phê duyệt giải trước khi Owner mở đăng ký
        // ══════════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PheDuyet(int id, string? ghiChu)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.Owner)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (giai == null) return NotFound();
            if (giai.TrangThai != "Draft")
            {
                TempData["Error"] = "Chỉ phê duyệt giải ở trạng thái Draft!";
                return RedirectToAction("Details", new { id });
            }

            giai.TrangThai = "Approved";
            await _context.SaveChangesAsync();

            await GhiLog("PheDuyetGiai", "GiaiDau", id,
                $"Phê duyệt giải '{giai.TenGiai}'. Ghi chú: {ghiChu}");

            // Thông báo cho Owner
            TempData["Success"] =
                $"✅ Đã phê duyệt giải '{giai.TenGiai}'. Owner có thể mở đăng ký.";
            return RedirectToAction("Details", new { id });
        }

        // ══════════════════════════════════════════════════════════
        // POST /AdminTournament/TuChoi/{id}
        // Draft → từ chối và xóa (Admin không duyệt)
        // ══════════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TuChoi(int id, string lyDo)
        {
            var giai = await _context.GiaiDaus
                .FirstOrDefaultAsync(g => g.Id == id);

            if (giai == null) return NotFound();
            if (giai.TrangThai != "Draft")
            {
                TempData["Error"] = "Chỉ từ chối giải ở trạng thái Draft!";
                return RedirectToAction("Details", new { id });
            }

            if (string.IsNullOrWhiteSpace(lyDo))
            {
                TempData["Error"] = "Cần nhập lý do từ chối!";
                return RedirectToAction("Details", new { id });
            }

            await GhiLog("TuChoiGiai", "GiaiDau", id,
                $"Từ chối giải '{giai.TenGiai}'. Lý do: {lyDo}");

            // Xóa giải (Draft chưa có dữ liệu quan trọng)
            _context.GiaiDaus.Remove(giai);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã từ chối và xóa giải '{giai.TenGiai}'.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════════
        // GET /AdminTournament/ExcelDoiSoat/5 — Admin cũng có thể tải Excel
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> ExcelDoiSoat(int id)
        {
            var (bytes, fileName) = await _excelService.ExportDoiSoat(id);
            await GhiLog("ExportExcelAdmin", "GiaiDau", id, "Admin xuất Excel đối soát");
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ══════════════════════════════════════════════════════════
        // GET /AdminTournament/BaoCao — Báo cáo doanh thu giải đấu
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> BaoCao()
        {
            var now = DateTime.Now;

            // Thống kê theo tháng (6 tháng gần nhất)
            var bieu6Thang = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var t = now.AddMonths(-i);
                var bd = new DateTime(t.Year, t.Month, 1);
                var kt = bd.AddMonths(1);

                var soGiai = await _context.GiaiDaus
                    .CountAsync(g => g.ThoiGianTao >= bd && g.ThoiGianTao < kt);

                var soDoiThanhToan = await _context.DoiBongs
                    .CountAsync(d => d.DaThanhToan
                              && d.ThoiGianThanhToan >= bd
                              && d.ThoiGianThanhToan < kt);

                var doanhThu = await _context.DoiBongs
                    .Where(d => d.DaThanhToan
                             && d.ThoiGianThanhToan >= bd
                             && d.ThoiGianThanhToan < kt)
                    .SumAsync(d => d.GiaiDau.LePhiGiai);

                bieu6Thang.Add(new
                {
                    thang = t.ToString("MM/yyyy"),
                    soGiai,
                    soDoiThanhToan,
                    doanhThu = (double)doanhThu
                });
            }
            ViewBag.Bieu6Thang = bieu6Thang;

            // Top Owner tổ chức nhiều giải nhất
            ViewBag.TopOwner = await _context.GiaiDaus
                .Where(g => g.TrangThai != "Draft")
                .GroupBy(g => g.Owner)
                .Select(g => new {
                    HoTen = g.Key.HoTen,
                    Email = g.Key.Email,
                    SoGiai = g.Count(),
                    SoActive = g.Count(x => x.TrangThai == "Active")
                })
                .OrderByDescending(x => x.SoGiai)
                .Take(10)
                .ToListAsync();

            // KPI tổng quan
            ViewBag.TongGiaiDau = await _context.GiaiDaus.CountAsync(g => g.TrangThai != "Draft");
            ViewBag.GiaiHoanThanh = await _context.GiaiDaus.CountAsync(g => g.TrangThai == "Finished");
            ViewBag.TongDoi = await _context.DoiBongs.CountAsync(d => d.DaThanhToan);
            ViewBag.TongTranDau = await _context.TranDaus.CountAsync(t => t.TrangThai == "Closed");
            ViewBag.TongDoanhThu = await _context.DoiBongs
                .Where(d => d.DaThanhToan)
                .SumAsync(d => d.GiaiDau.LePhiGiai);

            return View();
        }
    }
}