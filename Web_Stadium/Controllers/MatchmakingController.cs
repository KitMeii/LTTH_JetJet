using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;

namespace Web_Stadium.Controllers
{
    public class MatchmakingController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IRepository<Matchmaking> _repo;
        private readonly IConfiguration _config;

        public MatchmakingController(
            SanBongContext context,
            IRepository<Matchmaking> repo,
            IConfiguration config)
        {
            _context = context;
            _repo = repo;
            _config = config;
        }

        //GET: Matchmaking/Danh sách tin tìm đối thủ
        public async Task<IActionResult> Index(
           string? loaiSan, string? quan)
        {
            var query = _context.Matchmakings
                .Include(m => m.DatSan)
                    .ThenInclude(d => d.KhungGio)
                        .ThenInclude(k => k.SanBong)
                .Include(m => m.User)
                .Where(m => m.TrangThai == "DangTim")
                .OrderByDescending(m => m.NgayDang)
                .AsQueryable();

            if (!string.IsNullOrEmpty(loaiSan))
                query = query.Where(m =>
                    m.DatSan.KhungGio.SanBong.LoaiSan == loaiSan);

            if (!string.IsNullOrEmpty(quan))
                query = query.Where(m =>
                    m.DatSan.KhungGio.SanBong.Quan == quan);

            var list = await query.ToListAsync();

            ViewBag.LoaiSan = loaiSan;
            ViewBag.Quan = quan;
            ViewBag.TongTin = list.Count;
            ViewBag.CurrentUserId = TokenHelper.LayUserId(Request, _config);
            return View(list);
        }

        //GET /Matchmaking/Create? datSanId=1
        [YeuCauDangNhap]
        public async Task<IActionResult> Create(int datSanId)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var datSan = await _context.DatSans
                .Include(d => d.KhungGio)
                    .ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d =>
                    d.Id == datSanId &&
                    d.UserId == userId);

            if (datSan == null)
            {
                TempData["Error"] = "Không tìm thấy đơn đặt sân.";
                return RedirectToAction("Index");
            }

            //Kiểm tra đã đăng tin chưa
            var daCoTin = await _context.Matchmakings
                .AnyAsync(m => m.DatSanId == datSanId &&
                               m.TrangThai == "DangTim");
            if (daCoTin)
            {
                TempData["Error"] = "Đơn đặt sân này đã có tin tìm đội.";
                return RedirectToAction("Index");
            }
            ViewBag.DatSan = datSan;
            return View();
        }

        //POST /Matchmaking/Create
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> Create(
            int datSanId,
            string tieuDe,
            string? moTa,
            int soNguoiCanThem)
        {
            var userId = TokenHelper.LayUserId(Request, _config);

            var mm = new Matchmaking
            {
                DatSanId = datSanId,
                UserId = userId!.Value,
                TieuDe = tieuDe,
                MoTa = moTa,
                SoNguoiCanThem = soNguoiCanThem,
                TrangThai = "DangTim",
                NgayDang = DateTime.Now
            };

            await _repo.AddAsync(mm);
            TempData["Success"] = "Đã đăng tin tìm đội thành công!";
            return RedirectToAction("Index");
        }

        //POST /Matchmaking/DaDu - /đánh dấu đã đủ người
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> DaDu(int id)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var mm = await _repo.GetByIdAsync(id);

            if (mm == null || mm.UserId != userId)
            {
                return NotFound();
            }

            mm.TrangThai = "DaDu";
            await _repo.UpdateAsync(mm);
            TempData["Success"] = "Đã đánh dấu đủ người!";
            return RedirectToAction("Index");
        }

        //POST /Matchmaking/Dong - /Đóng tin
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> Dong(int id)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            var mm = await _repo.GetByIdAsync(id);

            if (mm == null || mm.UserId != userId)
                return NotFound();
            mm.TrangThai = "DaDong";
            await _repo.UpdateAsync(mm);
            TempData["Success"] = "Da huy tin tim doi!";
            // Neu tu MyBookings thi quay lai MyBookings, neu khong thi ve Index
            var referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("MyBookings"))
                return RedirectToAction("MyBookings", "Booking");
            return RedirectToAction("Index");
        }

        // GET /Matchmaking/GetDonCuaToi
        [YeuCauDangNhap]
        public async Task<IActionResult> GetDonCuaToi()
        {
            var userId = TokenHelper.LayUserId(Request, _config);

            var daTim = await _context.Matchmakings
                .Where(m => m.UserId == userId && m.TrangThai == "DangTim")
                .Select(m => m.DatSanId)
                .ToListAsync();

            var don = await _context.DatSans
                .Include(d => d.KhungGio)
                    .ThenInclude(k => k.SanBong)
                .Where(d => d.UserId == userId
                         && d.TrangThai == "DaXacNhan"
                         && d.NgayThiDau >= DateTime.Today
                         && !daTim.Contains(d.Id))
                .OrderBy(d => d.NgayThiDau)
                .ToListAsync();

            var result = don.Select(d => new {
                id = d.Id,
                tenSan = d.KhungGio != null && d.KhungGio.SanBong != null
                             ? d.KhungGio.SanBong.TenSan : "San khong xac dinh",
                diaChi = d.KhungGio != null && d.KhungGio.SanBong != null
                             ? d.KhungGio.SanBong.DiaChi + " - " + d.KhungGio.SanBong.Quan : "",
                ngayThiDau = d.NgayThiDau.ToString("dd/MM/yyyy"),
                gio = d.KhungGio != null
                             ? d.KhungGio.GioBatDau.ToString(@"hh\:mm")
                               + " - " + d.KhungGio.GioKetThuc.ToString(@"hh\:mm")
                             : ""
            });

            return Json(result);
        }
    }
}