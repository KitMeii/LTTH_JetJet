using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;

namespace Web_Stadium.Controllers
{
    public class VenuesController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IRepository<SanBong> _sanBongRepo;
        private readonly IConfiguration _config;

        public VenuesController(
            SanBongContext context,
            IRepository<SanBong> sanBongRepo,
            IConfiguration config)
        {
            _context = context;
            _sanBongRepo = sanBongRepo;
            _config = config;
        }

        // ══════════════════════════════════════════════════════════
        // GET: /Venues
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index(
            string? quan, string? loaiSan,
            string? loaiCo, string? tuKhoa,
            decimal? giaTu, decimal? giaDen,
            string? sapXep)
        {
            var query = _context.SanBongs
                .Where(s => s.TrangThaiDuyet == "DaDuyet" && !s.IsHidden);

            if (!string.IsNullOrEmpty(quan))
                query = query.Where(s => s.Quan == quan);
            if (!string.IsNullOrEmpty(loaiSan))
                query = query.Where(s => s.LoaiSan == loaiSan);
            if (!string.IsNullOrEmpty(loaiCo))
                query = query.Where(s => s.LoaiCo == loaiCo);
            if (!string.IsNullOrEmpty(tuKhoa))
                query = query.Where(s => s.TenSan.Contains(tuKhoa)
                                      || s.DiaChi.Contains(tuKhoa)
                                      || s.Quan.Contains(tuKhoa));

            query = sapXep switch
            {
                "gia_tang" => query.OrderBy(s => s.KhungGios.Min(k => k.Gia)),
                "gia_giam" => query.OrderByDescending(s => s.KhungGios.Min(k => k.Gia)),
                "danh_gia" => query.OrderByDescending(s => s.DanhGiaTrungBinh),
                _ => query.OrderByDescending(s => s.DanhGiaTrungBinh)
            };

            var danhSach = await query
                .Include(s => s.KhungGios)
                .ToListAsync();

            // Ảnh đại diện cho từng sân (ảnh có ThuTu nhỏ nhất — owner chọn ở QuanLyAnh)
            var sanIds = danhSach.Select(s => s.Id).ToList();
            var anhDaiDien = await _context.AnhSanBongs
                .Where(a => sanIds.Contains(a.SanBongId) && a.IsActive)
                .GroupBy(a => a.SanBongId)
                .Select(g => new
                {
                    SanBongId = g.Key,
                    DuongDan = g.OrderBy(a => a.ThuTu).First().DuongDan
                })
                .ToDictionaryAsync(k => k.SanBongId, v => v.DuongDan);
            ViewBag.AnhDaiDien = anhDaiDien;

            // Kiểm tra sân nào user đã bookmark (để hiện tim đỏ)
            var userId = TokenHelper.LayUserId(Request, _config);
            if (userId.HasValue)
            {
                var yeuThichIds = await _context.SanYeuThichs
                    .Where(s => s.UserId == userId.Value)
                    .Select(s => s.SanBongId)
                    .ToListAsync();
                ViewBag.YeuThichIds = yeuThichIds.ToHashSet();
            }
            else
            {
                ViewBag.YeuThichIds = new HashSet<int>();
            }

            ViewBag.DanhSachQuan = await _context.DanhMucQuans.Where(q => q.IsActive).OrderBy(q => q.ThuTu).ToListAsync();
            ViewBag.DanhSachLoaiSan = await _context.DanhMucLoaiSans.Where(l => l.IsActive).ToListAsync();
            ViewBag.DanhSachLoaiCo = await _context.DanhMucLoaiCos.Where(l => l.IsActive).ToListAsync();

            ViewBag.Quan = quan;
            ViewBag.LoaiSan = loaiSan;
            ViewBag.LoaiCo = loaiCo;
            ViewBag.TuKhoa = tuKhoa;
            ViewBag.SapXep = sapXep;
            ViewBag.UserId = userId;

            return View(danhSach);
        }

        // ══════════════════════════════════════════════════════════
        // GET: /Venues/Details/5
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Details(int id)
        {
            var sanBong = await _context.SanBongs
                .Include(s => s.KhungGios)
                .Include(s => s.DanhGia).ThenInclude(d => d.User)
                .Include(s => s.DichVus).ThenInclude(d => d.DanhMucDichVu)
                .FirstOrDefaultAsync(s => s.Id == id
                                       && s.TrangThaiDuyet == "DaDuyet"
                                       && !s.IsHidden);

            if (sanBong == null) return NotFound();

            // Cập nhật điểm TB
            if (sanBong.DanhGia.Any())
            {
                sanBong.DanhGiaTrungBinh = sanBong.DanhGia.Average(d => d.SoSao);
                await _context.SaveChangesAsync();
            }

            // Ảnh sân
            var anhSanBongs = await _context.AnhSanBongs
                .Where(a => a.SanBongId == id && a.IsActive)
                .OrderBy(a => a.ThuTu)
                .ToListAsync();
            ViewBag.AnhSanBongs = anhSanBongs;

            ViewBag.DichVus = sanBong.DichVus
                .Where(d => d.IsActive && d.TonKho > 0)
                .ToList();

            var userId = TokenHelper.LayUserId(Request, _config);
            if (userId.HasValue)
            {
                // Tìm đơn HoanThanh tại sân này chưa được đánh giá
                // (để biết datSanId cụ thể gắn vào đánh giá)
                var donChoGiaDanhGia = await _context.DatSans
                    .Include(d => d.KhungGio)
                    .Where(d => d.UserId == userId.Value
                             && d.KhungGio.SanBongId == id
                             && d.TrangThai == "HoanThanh"
                             && !_context.DanhGias.Any(dg => dg.DatSanId == d.Id))
                    .FirstOrDefaultAsync();

                ViewBag.DonChoGiaDanhGia = donChoGiaDanhGia; // null = không có đơn cần đánh giá
                ViewBag.DaDanhGia = donChoGiaDanhGia == null && await _context.DanhGias
                    .AnyAsync(dg => dg.UserId == userId.Value && dg.SanBongId == id);
                ViewBag.UserId = userId.Value;

                // Sân đã bookmark chưa
                ViewBag.DaYeuThich = await _context.SanYeuThichs
                    .AnyAsync(s => s.UserId == userId.Value && s.SanBongId == id);

                // Voucher user đang có (dùng ở Booking/Create)
                ViewBag.CoVoucher = await _context.UserVouchers
                    .AnyAsync(uv => uv.UserId == userId.Value
                                 && !uv.IsUsed
                                 && uv.NgayHetHan > DateTime.Now);
            }

            return View(sanBong);
        }

        // ══════════════════════════════════════════════════════════
        // POST: /Venues/DanhGiaSan
        // Đánh giá 3 tiêu chí + cộng +5 điểm thưởng
        // ══════════════════════════════════════════════════════════
        [HttpPost]
        [YeuCauDangNhap]
        public async Task<IActionResult> DanhGiaSan(
            int sanBongId,
            int datSanId,       // ← bắt buộc, lấy từ hidden field trong form
            int soSaoCo,        // tiêu chí 1: chất lượng mặt cỏ
            int soSaoCoSoVatChat, // tiêu chí 2
            int soSaoNhanVien,    // tiêu chí 3
            string? nhanXet)
        {
            var userId = TokenHelper.LayUserId(Request, _config);
            if (userId == null) return RedirectToAction("Login", "Auth");

            // Kiểm tra đơn thuộc về user + trạng thái HoanThanh
            var don = await _context.DatSans
                .Include(d => d.KhungGio)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && d.UserId == userId.Value
                                       && d.KhungGio.SanBongId == sanBongId
                                       && d.TrangThai == "HoanThanh");

            if (don == null)
            {
                TempData["Error"] = "Bạn chỉ có thể đánh giá sau khi đã hoàn thành trận đấu!";
                return RedirectToAction("Details", new { id = sanBongId });
            }

            // Mỗi đơn chỉ đánh giá 1 lần (ràng buộc UNIQUE trong DB)
            var daCoRoi = await _context.DanhGias
                .AnyAsync(dg => dg.DatSanId == datSanId);
            if (daCoRoi)
            {
                TempData["Error"] = "Đơn này đã được đánh giá rồi!";
                return RedirectToAction("Details", new { id = sanBongId });
            }

            // Tính điểm tổng = trung bình 3 tiêu chí
            var soSaoTrungBinh = (int)Math.Round((soSaoCo + soSaoCoSoVatChat + soSaoNhanVien) / 3.0);

            var danhGia = new DanhGia
            {
                UserId = userId.Value,
                SanBongId = sanBongId,
                DatSanId = datSanId,
                SoSao = Math.Clamp(soSaoTrungBinh, 1, 5),
                SoSaoCoSoVatChat = Math.Clamp(soSaoCoSoVatChat, 1, 5),
                SoSaoNhanVien = Math.Clamp(soSaoNhanVien, 1, 5),
                NhanXet = nhanXet?.Trim(),
                NgayDanhGia = DateTime.Now
            };
            _context.DanhGias.Add(danhGia);
            await _context.SaveChangesAsync();

            // Cập nhật điểm TB sân
            var allDG = await _context.DanhGias.Where(d => d.SanBongId == sanBongId).ToListAsync();
            var san = await _context.SanBongs.FindAsync(sanBongId);
            if (san != null)
            {
                san.DanhGiaTrungBinh = allDG.Average(d => d.SoSao);
                await _context.SaveChangesAsync();
            }

            // ✅ Cộng +5 điểm thưởng cho user
            await UserController.CongDiem(
                _context,
                userId.Value,
                soDiem: 5,
                loaiSuKien: "DanhGia",
                ghiChu: $"Đánh giá sân: {san?.TenSan}",
                datSanId: datSanId
            );

            TempData["Success"] = "Cảm ơn bạn đã đánh giá! Bạn nhận được +5 điểm thưởng ⭐";
            return RedirectToAction("Details", new { id = sanBongId });
        }
    }
}