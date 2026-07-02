using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Owner")]
    public class TournamentController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly TournamentService _tournamentService;
        private readonly StandingService _standingService;
        private readonly TournamentExcelService _excelService;

        public TournamentController(
            SanBongContext context,
            IConfiguration config,
            TournamentService tournamentService,
            StandingService standingService,
            TournamentExcelService excelService)
        {
            _context = context;
            _config = config;
            _tournamentService = tournamentService;
            _standingService = standingService;
            _excelService = excelService;
        }

        private int OwnerId() => TokenHelper.LayUserId(Request, _config)!.Value;

        private async Task GhiLog(string hanhDong, string doiTuong, int id, string moTa)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = OwnerId(),
                VaiTro = "Owner",
                HanhDong = hanhDong,
                DoiTuong = doiTuong,
                DoiTuongId = id,
                MoTa = moTa,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                ThoiGian = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        // ── GET /Tournament ──────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var list = await _context.GiaiDaus
                .Include(g => g.SanBong)
                .Include(g => g.DoiBongs)
                .Include(g => g.TranDaus)
                .Include(g => g.StaffPhuTrach)
                .Where(g => g.OwnerId == OwnerId())
                .OrderByDescending(g => g.ThoiGianTao)
                .ToListAsync();

            return View(list);
        }

        // ── GET /Tournament/Create ───────────────────────────────
        public async Task<IActionResult> Create()
        {
            ViewBag.SanList = await _context.SanBongs
                .Where(s => s.OwnerId == OwnerId()
                         && s.TrangThaiDuyet == "DaDuyet"
                         && !s.IsHidden)
                .ToListAsync();
            return View();
        }

        // ── POST /Tournament/Create ──────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGiaiDauDto dto)
        {
            var (ok, error, giai) = await _tournamentService.TaoGiaiDau(dto, OwnerId());
            if (!ok)
            {
                TempData["Error"] = error;
                ViewBag.SanList = await _context.SanBongs
                    .Where(s => s.OwnerId == OwnerId()
                             && s.TrangThaiDuyet == "DaDuyet"
                             && !s.IsHidden).ToListAsync();
                return View(dto);
            }

            await GhiLog("TaoGiaiDau", "GiaiDau", giai!.Id, $"Tạo giải '{giai.TenGiai}'");
            TempData["Success"] = $"Tạo giải '{giai.TenGiai}' thành công!";
            return RedirectToAction("Details", new { id = giai.Id });
        }

        // ── GET /Tournament/LayKhungGioJson?sanId=...&tu=...&den=... ─
        // Trả JSON khung giờ của sân để FullCalendar render
        [HttpGet]
        public async Task<IActionResult> LayKhungGioJson(int sanId)
        {
            var san = await _context.SanBongs.FirstOrDefaultAsync(s =>
                s.Id == sanId && s.OwnerId == OwnerId() && s.TrangThaiDuyet == "DaDuyet");
            if (san == null) return Json(new { ok = false, message = "Sân không hợp lệ!" });

            var khungGios = await _context.KhungGios
                .Where(k => k.SanBongId == sanId)
                .OrderBy(k => k.GioBatDau)
                .Select(k => new
                {
                    id = k.Id,
                    gioBatDau = k.GioBatDau.ToString(@"hh\:mm"),
                    gioKetThuc = k.GioKetThuc.ToString(@"hh\:mm"),
                    gia = k.Gia,
                    loaiNgay = k.LoaiNgay
                })
                .ToListAsync();
            return Json(new { ok = true, khungGios });
        }

        // ── GET /Tournament/Details/5 ────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var ownerId = OwnerId();
            var giai = await _context.GiaiDaus
                .Include(g => g.SanBong).ThenInclude(s => s!.KhungGios)
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs).ThenInclude(d => d.ThanhViens)
                .Include(g => g.DoiBongs).ThenInclude(d => d.Bang)
                .Include(g => g.DoiBongs).ThenInclude(d => d.DoiTruong)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiNha)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiKhach)
                .Include(g => g.TranDaus).ThenInclude(t => t.SuKiens)
                .Include(g => g.TranDaus).ThenInclude(t => t.KhungGio)
                .Include(g => g.StaffPhuTrach)
                .FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == ownerId);

            if (giai == null) return NotFound();

            ViewBag.BangXepHang = await _standingService.GetStandings(id);

            // Staff khả dụng (được phân công tại sân của giải)
            ViewBag.StaffKhaDung = await _context.StaffSanPhanCongs
                .Include(p => p.Staff)
                .Where(p => p.SanBongId == giai.SanBongId && p.Staff.IsActive)
                .Select(p => p.Staff)
                .Distinct()
                .ToListAsync();

            return View(giai);
        }

        // ── POST /Tournament/MoiDangKy ───────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MoiDangKy(int id)
        {
            var (ok, error) = await _tournamentService.MoiDangKy(id, OwnerId());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("Details", new { id }); }

            await GhiLog("MoiDangKy", "GiaiDau", id, "Mở đăng ký giải");
            TempData["Success"] = "Đã mở đăng ký!";
            return RedirectToAction("Details", new { id });
        }

        // ── POST /Tournament/DongDangKy ──────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DongDangKy(int id)
        {
            var (ok, error) = await _tournamentService.DongDangKy(id, OwnerId());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("Details", new { id }); }

            await GhiLog("DongDangKy", "GiaiDau", id, "Đóng đăng ký giải");
            TempData["Success"] = "Đã đóng đăng ký! Tiến hành chia bảng.";
            return RedirectToAction("ChiaBang", new { id });
        }

        // ── POST /Tournament/XacNhanThanhToanDoi ─────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanThanhToanDoi(int doiId)
        {
            var (ok, error, doi) = await _tournamentService.XacNhanThanhToanDoi(doiId, OwnerId());
            if (!ok)
            {
                TempData["Error"] = error;
                var giai = await _context.DoiBongs.Where(d => d.Id == doiId).Select(d => d.GiaiDauId).FirstOrDefaultAsync();
                return RedirectToAction("Details", new { id = giai });
            }

            await GhiLog("XacNhanThanhToan", "DoiBong", doiId,
                $"Xác nhận thanh toán cho đội '{doi!.TenDoi}'");
            TempData["Success"] = $"Đã xác nhận thanh toán cho đội '{doi.TenDoi}'!";
            return RedirectToAction("Details", new { id = doi.GiaiDauId });
        }

        // ── POST /Tournament/HuyDangKyDoi ────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> HuyDangKyDoi(int doiId, string lyDo)
        {
            var (ok, error, giaiId) = await _tournamentService.HuyDangKyDoi(doiId, lyDo ?? "", OwnerId());
            if (!ok)
            {
                TempData["Error"] = error;
                var gid = await _context.DoiBongs.Where(d => d.Id == doiId).Select(d => d.GiaiDauId).FirstOrDefaultAsync();
                return RedirectToAction("Details", new { id = gid });
            }

            await GhiLog("HuyDangKyDoi", "DoiBong", doiId, $"Hủy đội. Lý do: {lyDo}");
            TempData["Success"] = "Đã hủy đăng ký đội.";
            return RedirectToAction("Details", new { id = giaiId });
        }

        // ── GET /Tournament/ChiaBang/5 ───────────────────────────
        public async Task<IActionResult> ChiaBang(int id)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs).ThenInclude(d => d.Bang)
                .Include(g => g.DoiBongs).ThenInclude(d => d.DoiTruong)
                .Include(g => g.DoiBongs).ThenInclude(d => d.ThanhViens)
                .FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == OwnerId());

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
            var (ok, error) = await _tournamentService.GanDoiVaoBang(doiId, bangId, OwnerId());
            if (!ok) return Json(new { ok = false, message = error });

            var doi = await _context.DoiBongs.FindAsync(doiId);
            var bang = bangId.HasValue
                ? await _context.BangDaus.FindAsync(bangId)
                : null;

            return Json(new { ok = true, tenDoi = doi?.TenDoi, tenBang = bang?.TenBang });
        }

        // ── POST /Tournament/KhoiTao ─────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KhoiTao(int id)
        {
            var (ok, error) = await _tournamentService.KhoiTaoGiai(id, OwnerId());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("ChiaBang", new { id }); }

            await GhiLog("KhoiTaoGiai", "GiaiDau", id, "Khởi tạo giải và sinh lịch thi đấu");
            TempData["Success"] = "Khởi tạo thành công! Email lịch đấu đã gửi cho các đội.";
            return RedirectToAction("Details", new { id });
        }

        // ── POST /Tournament/GanStaff ────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GanStaff(int giaiDauId, int staffId)
        {
            var (ok, error) = await _tournamentService.GanStaffPhuTrach(giaiDauId, staffId, OwnerId());
            if (!ok) { TempData["Error"] = error; }
            else
            {
                await GhiLog("GanStaff", "GiaiDau", giaiDauId, $"Gán Staff {staffId} phụ trách giải");
                TempData["Success"] = "Đã gán Staff phụ trách!";
            }
            return RedirectToAction("Details", new { id = giaiDauId });
        }

        // ── POST /Tournament/GanKhungGio (AJAX) ──────────────────
        [HttpPost]
        public async Task<IActionResult> GanKhungGio(int tranDauId, int khungGioId, string ngayThiDauStr)
        {
            if (!DateTime.TryParse(ngayThiDauStr, out var ngay))
                return Json(new { ok = false, message = "Ngày không hợp lệ!" });

            var (ok, error, kg, ng) = await _tournamentService.GanKhungGioTran(tranDauId, khungGioId, ngay, OwnerId());
            if (!ok) return Json(new { ok = false, message = error });

            return Json(new
            {
                ok = true,
                gioBD = kg!.GioBatDau.ToString(@"hh\:mm"),
                gioKT = kg.GioKetThuc.ToString(@"hh\:mm"),
                ngay = ng.ToString("dd/MM/yyyy")
            });
        }

        // ── POST /Tournament/SinhBracket ─────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SinhBracket(int giaiDauId)
        {
            var (ok, error) = await _tournamentService.SinhVongKnockOut(giaiDauId, OwnerId());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("Details", new { id = giaiDauId }); }

            await GhiLog("SinhBracket", "GiaiDau", giaiDauId, "Sinh vòng knock-out");
            TempData["Success"] = "Đã sinh lịch vòng knock-out!";
            return RedirectToAction("Bracket", new { id = giaiDauId });
        }

        // ── GET /Tournament/Bracket/5 ────────────────────────────
        public async Task<IActionResult> Bracket(int id)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.DoiBongs)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiNha)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiKhach)
                .Include(g => g.TranDaus).ThenInclude(t => t.KhungGio)
                .Include(g => g.SanBong).ThenInclude(s => s!.KhungGios)
                .FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == OwnerId());
            if (giai == null) return NotFound();

            ViewBag.IsOwner = true;
            ViewBag.KhungGios = giai.SanBong?.KhungGios.OrderBy(k => k.GioBatDau).ToList()
                                ?? new List<KhungGio>();
            return View(giai);
        }

        // ── GET /Tournament/LichBlock/5 ──────────────────────────
        public async Task<IActionResult> LichBlock(int id)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.SanBong).ThenInclude(s => s!.KhungGios)
                .FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == OwnerId());
            if (giai == null) return NotFound();
            return View(giai);
        }

        // ── POST /Tournament/LuuLichBlock (AJAX) ─────────────────
        [HttpPost]
        public async Task<IActionResult> LuuLichBlock(int giaiId, string lichBlockJson)
        {
            List<ScheduleService.SlotKhungGio>? lichBlock;
            try
            {
                lichBlock = JsonSerializer.Deserialize<List<ScheduleService.SlotKhungGio>>(
                    lichBlockJson ?? "[]",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return Json(new { ok = false, message = "Dữ liệu không hợp lệ!" });
            }
            if (lichBlock == null) lichBlock = new();

            var (ok, error) = await _tournamentService.CapNhatLichBlock(giaiId, OwnerId(), lichBlock);
            if (!ok) return Json(new { ok = false, message = error });

            await GhiLog("CapNhatLichBlock", "GiaiDau", giaiId, $"Cập nhật {lichBlock.Count} slot block");
            return Json(new { ok = true, soSlot = lichBlock.Count });
        }

        // ── POST /Tournament/KetThuc ─────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> KetThuc(int id)
        {
            var (ok, error) = await _tournamentService.KetThucGiai(id, OwnerId());
            if (!ok) { TempData["Error"] = error; return RedirectToAction("Details", new { id }); }

            await GhiLog("KetThucGiai", "GiaiDau", id, "Kết thúc giải đấu");
            TempData["Success"] = "Giải kết thúc! Tải Excel đối soát bên dưới.";
            return RedirectToAction("Details", new { id });
        }

        // ── POST /Tournament/XuLySuCo ────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLySuCo(int tranDauId, int doiBoCuocId, string lyDo)
        {
            var (ok, error) = await _tournamentService.XuLySuCo(
                tranDauId, doiBoCuocId, lyDo, OwnerId());

            var tran = await _context.TranDaus.FindAsync(tranDauId);
            if (!ok) { TempData["Error"] = error; }
            else
            {
                await GhiLog("XuLySuCo", "TranDau", tranDauId,
                    $"Đội {doiBoCuocId} bỏ cuộc. Lý do: {lyDo}");
                TempData["Success"] = "Đã xử lý sự cố. Đội vi phạm thua 0-3.";
            }

            return RedirectToAction("Details", new { id = tran?.GiaiDauId });
        }

        // ── GET /Tournament/ExcelDoiSoat/5 ──────────────────────
        public async Task<IActionResult> ExcelDoiSoat(int id)
        {
            var giai = await _context.GiaiDaus
                .FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == OwnerId());
            if (giai == null) return NotFound();

            var (bytes, fileName) = await _excelService.ExportDoiSoat(id);
            await GhiLog("ExportExcel", "GiaiDau", id, "Xuất Excel đối soát tài chính");

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}
