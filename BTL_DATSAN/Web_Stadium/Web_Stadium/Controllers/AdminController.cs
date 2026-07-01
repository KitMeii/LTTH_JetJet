using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Admin")]
    public class AdminController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;

        public AdminController(SanBongContext context, IConfiguration config, EmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        private int GetAdminId() => TokenHelper.LayUserId(Request, _config)!.Value;

        private async Task GhiLog(string hanhDong, string doiTuong, int doiTuongId, string moTa)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = GetAdminId(),
                VaiTro = "Admin",
                HanhDong = hanhDong,
                DoiTuong = doiTuong,
                DoiTuongId = doiTuongId,
                MoTa = moTa,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // DASHBOARD
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var thangNay = new DateTime(now.Year, now.Month, 1);

            ViewBag.TongSan = await _context.SanBongs.CountAsync();
            ViewBag.TongUser = await _context.Users.CountAsync();
            ViewBag.TongDatSan = await _context.DatSans.CountAsync();
            ViewBag.ChoPheDuyet = await _context.SanBongs
                .CountAsync(s => s.TrangThaiDuyet == "ChoDuyet");
            ViewBag.KhieuNaiChoXuLy = await _context.KhieuNais
                .CountAsync(k => k.TrangThai == "ChoXuLy");

            // Doanh thu PitchHub tháng này (hoa hồng thực thu)
            // Pre-load tỷ lệ hoa hồng một lần, tránh N+1 query
            var tyLeMapIndex = await _context.DanhMucQuans
                .AsNoTracking()
                .Include(q => q.VungKhuVuc)
                .Where(q => q.VungKhuVuc != null)
                .ToDictionaryAsync(q => q.TenQuan, q => q.VungKhuVuc!.TyLeHoaHong);

            var datSansThang = await _context.DatSans
                .AsNoTracking()
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => d.ThoiGianTao >= thangNay
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                          || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy"))
                .ToListAsync();

            ViewBag.DoanhThuHoaHong = datSansThang.Sum(d => TinhPhiHoaHong(d, tyLeMapIndex));
            ViewBag.DoanhThuSan = datSansThang.Sum(d => TinhDoanhThuPhaSinh(d));

            // Biểu đồ 6 tháng — load toàn bộ 6 tháng một lần thay vì 6 query riêng
            var start6Thang = now.AddMonths(-5);
            var start6ThangBd = new DateTime(start6Thang.Year, start6Thang.Month, 1);
            var allRows6Thang = await _context.DatSans
                .AsNoTracking()
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => d.ThoiGianTao >= start6ThangBd
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                          || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy"))
                .ToListAsync();

            var bieu6Thang = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var t = now.AddMonths(-i);
                var bd = new DateTime(t.Year, t.Month, 1);
                var kt = bd.AddMonths(1);
                var rows = allRows6Thang.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToList();
                var phi = rows.Sum(d => TinhPhiHoaHong(d, tyLeMapIndex));
                bieu6Thang.Add(new { thang = t.ToString("MM/yyyy"), phi = (double)phi });
            }
            ViewBag.Bieu6Thang = bieu6Thang;

            ViewBag.TopSan = await _context.SanBongs
                .AsNoTracking()
                .Include(s => s.KhungGios).ThenInclude(k => k.DatSans)
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .Select(s => new
                {
                    Ten = s.TenSan,
                    SoLuot = s.KhungGios.SelectMany(k => k.DatSans).Count()
                })
                .OrderByDescending(x => x.SoLuot).Take(5).ToListAsync();

            // Top 5 Owner có nhiều sân nhất
            ViewBag.TopOwners = await _context.Users
                .AsNoTracking()
                .Where(u => u.VaiTro == "Owner" && u.IsActive)
                .Select(u => new {
                    u.HoTen,
                    u.Email,
                    SoSan = u.SanBongs.Count(s => s.TrangThaiDuyet == "DaDuyet")
                })
                .OrderByDescending(u => u.SoSan)
                .Take(5)
                .ToListAsync();

            // 10 hoạt động gần nhất
            ViewBag.RecentActivity = await _context.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.ThoiGian)
                .Take(10)
                .Select(a => new {
                    a.HanhDong,
                    a.MoTa,
                    a.ThoiGian
                })
                .ToListAsync();

            // Biểu đồ theo quý — load toàn bộ năm hiện tại một lần
            {
                var bdNam = new DateTime(now.Year, 1, 1);
                var ktNam = new DateTime(now.Year + 1, 1, 1);
                var allRowsNam = await _context.DatSans
                    .AsNoTracking()
                    .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                    .Where(d => d.ThoiGianTao >= bdNam && d.ThoiGianTao < ktNam
                             && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                              || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy"))
                    .ToListAsync();
                var dataQuy = new List<object>();
                for (int q = 1; q <= 4; q++)
                {
                    var bd = new DateTime(now.Year, (q - 1) * 3 + 1, 1);
                    var kt = bd.AddMonths(3);
                    var rows = allRowsNam.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToList();
                    dataQuy.Add(BuildDataPoint($"Quý {q}", rows, tyLeMapIndex));
                }
                ViewBag.DoanhThuTheoQuy = dataQuy;
            }

            return View();
        }

        // Helper tính doanh thu thực phát sinh theo kịch bản KA / KB
        private decimal TinhDoanhThuPhaSinh(DatSan d)
        {
            // KA: Khách hủy — doanh thu = TienCoc
            if (d.TrangThai == "DaHuy") return d.TienCoc;
            // KB: Khách đến đá đủ — doanh thu = TongTien (nếu = 0 thì dùng TienCoc)
            return d.TongTien > 0 ? d.TongTien : d.TienCoc;
        }

        private decimal TinhPhiHoaHong(DatSan d, Dictionary<string, decimal>? tyLeMap = null)
        {
            var dt = TinhDoanhThuPhaSinh(d);
            var tenQuan = d.KhungGio?.SanBong?.Quan;

            decimal tyLe = 0;
            if (tyLeMap != null)
                tyLeMap.TryGetValue(tenQuan ?? "", out tyLe);
            else
                tyLe = _context.DanhMucQuans
                    .Where(q => q.TenQuan == tenQuan)
                    .Select(q => q.VungKhuVuc.TyLeHoaHong)
                    .FirstOrDefault();

            return dt * (tyLe == 0 ? 0.10m : tyLe);
        }

        // ══════════════════════════════════════════════════════════
        // MASTER DATA — bao gồm quản lý Vùng
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> MasterData()
        {
            ViewBag.Vungs = await _context.VungKhuVucs
                .Include(v => v.DanhMucQuans)
                .OrderBy(v => v.ThuTu).ToListAsync();
            ViewBag.Quans = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .OrderBy(q => q.ThuTu).ToListAsync();
            ViewBag.LoaiSans = await _context.DanhMucLoaiSans.ToListAsync();
            ViewBag.LoaiCos = await _context.DanhMucLoaiCos.ToListAsync();
            ViewBag.DichVus = await _context.DanhMucDichVus.ToListAsync();
            return View();
        }

        // ── Vùng khu vực ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemVung(string tenVung, string? moTa,
            decimal tyLeHoaHong, string mauSac, double lat, double lng, int thuTu)
        {
            if (string.IsNullOrWhiteSpace(tenVung))
            { TempData["Error"] = "Tên vùng không được để trống."; return RedirectToAction("MasterData"); }
            if (await _context.VungKhuVucs.AnyAsync(v => v.TenVung == tenVung))
            { TempData["Error"] = $"Vùng \"{tenVung}\" đã tồn tại."; return RedirectToAction("MasterData"); }

            var vung = new VungKhuVuc
            {
                TenVung = tenVung,
                MoTa = moTa,
                TyLeHoaHong = tyLeHoaHong,
                MauSac = mauSac,
                Lat = lat,
                Lng = lng,
                ThuTu = thuTu
            };
            _context.VungKhuVucs.Add(vung);
            await _context.SaveChangesAsync();
            await GhiLog("ThemVung", "VungKhuVuc", vung.Id, $"Thêm vùng: {tenVung} — {tyLeHoaHong:P0}");
            TempData["Success"] = $"Đã thêm vùng \"{tenVung}\" ({tyLeHoaHong:P0})!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> SuaVung(int id, decimal tyLeHoaHong, string? moTa, string mauSac)
        {
            var vung = await _context.VungKhuVucs.FindAsync(id);
            if (vung == null) return NotFound();
            var oldTyLe = vung.TyLeHoaHong;
            vung.TyLeHoaHong = tyLeHoaHong;
            vung.MoTa = moTa;
            vung.MauSac = mauSac;
            await _context.SaveChangesAsync();
            await GhiLog("SuaVung", "VungKhuVuc", id,
                $"Sửa vùng {vung.TenVung}: {oldTyLe:P0} → {tyLeHoaHong:P0}");
            TempData["Success"] = $"Đã cập nhật vùng \"{vung.TenVung}\": {tyLeHoaHong:P0}";
            return RedirectToAction("MasterData");
        }

        // ── Gán Quận vào Vùng ────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> GanQuanVaoVung(int quanId, int? vungId)
        {
            var quan = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc).FirstOrDefaultAsync(q => q.Id == quanId);
            if (quan == null) return NotFound();
            var oldVung = quan.VungKhuVuc?.TenVung ?? "Chưa có";
            quan.VungKhuVucId = vungId;
            await _context.SaveChangesAsync();
            var newVung = vungId.HasValue
                ? (await _context.VungKhuVucs.FindAsync(vungId))?.TenVung : "Bỏ gán";
            await GhiLog("GanQuanVaoVung", "DanhMucQuan", quanId,
                $"{quan.TenQuan}: {oldVung} → {newVung}");
            TempData["Success"] = $"Đã gán \"{quan.TenQuan}\" vào vùng \"{newVung}\"";
            return RedirectToAction("MasterData");
        }

        // AJAX — tra tỷ lệ hoa hồng theo QuanId (dùng trong form đăng ký sân của Owner)
        [HttpGet]
        public async Task<IActionResult> GetTyLeTheoQuan(int quanId)
        {
            var quan = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .FirstOrDefaultAsync(q => q.Id == quanId);
            if (quan?.VungKhuVuc == null)
                return Json(new { tyLe = 0.10, tenVung = "Chưa phân vùng", mauSac = "#aaa" });
            return Json(new
            {
                tyLe = (double)quan.VungKhuVuc.TyLeHoaHong,
                tenVung = quan.VungKhuVuc.TenVung,
                mauSac = quan.VungKhuVuc.MauSac
            });
        }

        // ── Quận / Huyện ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemQuan(string tenQuan, string thanhPho, int thuTu)
        {
            if (string.IsNullOrWhiteSpace(tenQuan))
            { TempData["Error"] = "Tên quận không được để trống."; return RedirectToAction("MasterData"); }
            if (await _context.DanhMucQuans.AnyAsync(q => q.TenQuan == tenQuan))
            { TempData["Error"] = $"Quận \"{tenQuan}\" đã tồn tại."; return RedirectToAction("MasterData"); }
            var q = new DanhMucQuan { TenQuan = tenQuan, ThanhPho = thanhPho, ThuTu = thuTu };
            _context.DanhMucQuans.Add(q);
            await _context.SaveChangesAsync();
            await GhiLog("ThemQuan", "DanhMucQuan", q.Id, $"Thêm quận: {tenQuan}");
            TempData["Success"] = $"Đã thêm quận \"{tenQuan}\"!";
            return RedirectToAction("MasterData");
        }

        [HttpGet]
        public async Task<IActionResult> ToggleQuan(int id)
        {
            var q = await _context.DanhMucQuans.FindAsync(id);
            if (q == null) return NotFound();
            q.IsActive = !q.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{(q.IsActive ? "Bật" : "Tắt")} quận \"{q.TenQuan}\"";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiQuan(int id, string isActive)
        {
            var q = await _context.DanhMucQuans.FindAsync(id);
            if (q == null) return NotFound();
            bool active = isActive?.ToLower() == "true";
            q.IsActive = active;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{(active ? "Bật" : "Tắt")} quận \"{q.TenQuan}\"";
            return RedirectToAction("MasterData");
        }

        // ── Loại Sân ────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemLoaiSan(string maLoai, string tenLoai)
        {
            if (await _context.DanhMucLoaiSans.AnyAsync(l => l.MaLoai == maLoai))
            { TempData["Error"] = $"Mã \"{maLoai}\" đã tồn tại."; return RedirectToAction("MasterData"); }
            var l = new DanhMucLoaiSan { MaLoai = maLoai, TenLoai = tenLoai };
            _context.DanhMucLoaiSans.Add(l);
            await _context.SaveChangesAsync();
            await GhiLog("ThemLoaiSan", "DanhMucLoaiSan", l.Id, $"Thêm: {maLoai}");
            TempData["Success"] = $"Đã thêm loại sân \"{tenLoai}\"!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiLoaiSan(int id, string isActive)
        {
            var l = await _context.DanhMucLoaiSans.FindAsync(id);
            if (l == null) return NotFound();
            bool active = isActive?.ToLower() == "true";
            l.IsActive = active; await _context.SaveChangesAsync();
            TempData["Success"] = $"{(active ? "Bật" : "Tắt")} loại sân \"{l.TenLoai}\"";
            return RedirectToAction("MasterData");
        }

        // ── Loại Cỏ ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemLoaiCo(string maLoai, string tenLoai)
        {
            if (await _context.DanhMucLoaiCos.AnyAsync(l => l.MaLoai == maLoai))
            { TempData["Error"] = $"Mã \"{maLoai}\" đã tồn tại."; return RedirectToAction("MasterData"); }
            var l = new DanhMucLoaiCo { MaLoai = maLoai, TenLoai = tenLoai };
            _context.DanhMucLoaiCos.Add(l);
            await _context.SaveChangesAsync();
            await GhiLog("ThemLoaiCo", "DanhMucLoaiCo", l.Id, $"Thêm: {maLoai}");
            TempData["Success"] = $"Đã thêm loại cỏ \"{tenLoai}\"!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiLoaiCo(int id, string isActive)
        {
            var l = await _context.DanhMucLoaiCos.FindAsync(id);
            if (l == null) return NotFound();
            bool active = isActive?.ToLower() == "true";
            l.IsActive = active; await _context.SaveChangesAsync();
            TempData["Success"] = $"{(active ? "Bật" : "Tắt")} loại cỏ \"{l.TenLoai}\"";
            return RedirectToAction("MasterData");
        }

        // ── Danh mục dịch vụ ────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemDanhMucDichVu(string tenDichVu, string? icon, string? moTa)
        {
            if (string.IsNullOrWhiteSpace(tenDichVu))
            { TempData["Error"] = "Tên dịch vụ không được để trống."; return RedirectToAction("MasterData"); }
            var dv = new DanhMucDichVu { TenDichVu = tenDichVu, Icon = icon, MoTa = moTa };
            _context.DanhMucDichVus.Add(dv);
            await _context.SaveChangesAsync();
            await GhiLog("ThemDanhMucDichVu", "DanhMucDichVu", dv.Id, $"Thêm: {tenDichVu}");
            TempData["Success"] = $"Đã thêm dịch vụ \"{tenDichVu}\"!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiDichVu(int id, string isActive)
        {
            var dv = await _context.DanhMucDichVus.FindAsync(id);
            if (dv == null) return NotFound();
            bool active = isActive?.ToLower() == "true";
            dv.IsActive = active; await _context.SaveChangesAsync();
            TempData["Success"] = $"{(active ? "Bật" : "Tắt")} \"{dv.TenDichVu}\"";
            return RedirectToAction("MasterData");
        }

        // ══════════════════════════════════════════════════════════
        // FRANCHISE — Duyệt sân + khoá/mở TK
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> DuyetSan()
        {
            var list = await _context.SanBongs
                .Include(s => s.Owner)
                .Where(s => s.TrangThaiDuyet == "ChoDuyet")
                .OrderBy(s => s.Id).ToListAsync();

            ViewBag.TyLeMap = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.VungKhuVuc != null)
                .ToDictionaryAsync(q => q.TenQuan, q => q.VungKhuVuc!.TyLeHoaHong);

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> PheDuyet(int id, string trangThai, decimal? tyLeOverride, string? lyDoTuChoi)
        {
            if (!string.IsNullOrEmpty(lyDoTuChoi))
                TempData["LyDoTuChoi"] = lyDoTuChoi;
            var san = await _context.SanBongs
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (san == null) return NotFound();

            san.TrangThaiDuyet = trangThai;

            // Tìm đúng 1 bản ghi VungKhuVuc của sân này
            var vungKhuVuc = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.TenQuan == san.Quan && q.VungKhuVuc != null)
                .Select(q => q.VungKhuVuc)
                .FirstOrDefaultAsync();

            // Admin override tỷ lệ hoa hồng trước khi duyệt (nếu cần)
            if (tyLeOverride.HasValue && tyLeOverride > 0 && vungKhuVuc != null)
                vungKhuVuc.TyLeHoaHong = tyLeOverride.Value;

            // Lấy tyLe để log
            var tyLeLog = vungKhuVuc?.TyLeHoaHong ?? 0.10m;

            // Kích hoạt Owner khi duyệt sân đầu tiên
            if (trangThai == "DaDuyet" && san.Owner != null && !san.Owner.IsActive)
            {
                san.Owner.IsActive = true;
                await GhiLog("KichHoatOwner", "User", san.Owner.Id,
                    $"Kích hoạt Owner {san.Owner.HoTen} khi duyệt sân đầu tiên");
            }

            await _context.SaveChangesAsync();

            await GhiLog(trangThai == "DaDuyet" ? "PheDuyetSan" : "TuChoiSan",
                "SanBong", id, $"{trangThai}: {san.TenSan} — HH: {tyLeLog:P0}");

            // ✅ Gửi email thông báo cho Owner
            if (san.Owner != null && !string.IsNullOrEmpty(san.Owner.Email))
            {
                if (trangThai == "DaDuyet")
                {
                    await _emailService.GuiEmailAsync(
                        san.Owner.Email,
                        san.Owner.HoTen,
                        $"✅ Sân \"{san.TenSan}\" đã được phê duyệt — PitchHub", // ĐÃ FIX: Escape dấu ngoặc kép bằng \"
                        $@"<div style='font-family:Arial,sans-serif;max-width:560px;margin:0 auto;'>
                <div style='background:linear-gradient(135deg,#0f2027,#1a3a2a);padding:28px;text-align:center;border-radius:16px 16px 0 0;'>
                    <div style='font-size:1.8rem;font-weight:900;color:#fff;'>PITCH<span style='color:#1ed760;'>HUB</span>⚽</div>
                </div>
                <div style='padding:28px;background:#fff;border-radius:0 0 16px 16px;'>
                    <h2 style='color:#1ed760;'>🎉 Sân đã được phê duyệt!</h2>
                    <p>Xin chào <strong>{san.Owner.HoTen}</strong>,</p>
                    <p>Sân <strong>{san.TenSan}</strong> đã được Admin PitchHub phê duyệt thành công.</p>
                    <div style='background:#f8fffe;border:1px solid #1ed760;border-radius:10px;padding:16px;margin:16px 0;'>
                        <p>📍 Địa chỉ: {san.DiaChi}, {san.Quan}</p>
                        <p>💰 Tỷ lệ hoa hồng: <strong>{tyLeLog:P0}</strong></p>
                        <p>📅 Ngày duyệt: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                    </div>
                    <p>Bạn có thể bắt đầu cấu hình khung giờ, bảng giá và đưa sân vào hoạt động ngay!</p>
                    <a href='https://pitchhub.vn/Owner' style='display:inline-block;background:#1ed760;color:#000;padding:12px 28px;border-radius:10px;text-decoration:none;font-weight:700;'>Vào quản lý sân →</a>
                </div>
            </div>"
                    );
                }
                else
                {
                    var lyDo = TempData["LyDoTuChoi"]?.ToString() ?? "Hồ sơ chưa đầy đủ hoặc không phù hợp";
                    await _emailService.GuiEmailAsync(
                        san.Owner.Email,
                        san.Owner.HoTen,
                        $"❌ Sân \"{san.TenSan}\" chưa được phê duyệt — PitchHub", // ĐÃ FIX: Escape dấu ngoặc kép bằng \"
                        $@"<div style='font-family:Arial,sans-serif;max-width:560px;margin:0 auto;'>
                <div style='background:linear-gradient(135deg,#1a0a0a,#2a1010);padding:28px;text-align:center;border-radius:16px 16px 0 0;'>
                    <div style='font-size:1.8rem;font-weight:900;color:#fff;'>PITCH<span style='color:#1ed760;'>HUB</span>⚽</div>
                </div>
                <div style='padding:28px;background:#fff;border-radius:0 0 16px 16px;'>
                    <h2 style='color:#e74c3c;'>Hồ sơ chưa được duyệt</h2>
                    <p>Xin chào <strong>{san.Owner.HoTen}</strong>,</p>
                    <p>Sân <strong>{san.TenSan}</strong> chưa được phê duyệt vào thời điểm này.</p>
                    <div style='background:#fff8f0;border:1px solid #f59e0b;border-radius:10px;padding:16px;margin:16px 0;'>
                        <p><strong>Lý do:</strong> {lyDo}</p>
                    </div>
                    <p>Bạn có thể bổ sung thông tin và gửi lại hồ sơ. Nếu cần hỗ trợ, vui lòng liên hệ support@pitchhub.vn</p>
                </div>
            </div>"
                    );
                }
            }

            TempData["Success"] = trangThai == "DaDuyet"
                ? $"Đã duyệt \"{san.TenSan}\" (hoa hồng {tyLeLog:P0}) — Email đã gửi cho Owner"
                : $"Đã từ chối \"{san.TenSan}\" — Email đã gửi cho Owner";

            return RedirectToAction("DuyetSan");
        }

        // Xem hợp đồng Owner đã ký (trong trang DuyetSan)
        public async Task<IActionResult> XemHopDong(int id)
        {
            var san = await _context.SanBongs
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (san == null) return NotFound();
            return View(san);
        }

        // Admin xem chi tiết đơn đặt sân (từ AuditLog)
        public async Task<IActionResult> ChiTietDon(int id)
        {
            var don = await _context.DatSans
                .AsNoTracking()
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong).ThenInclude(s => s.Owner)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Include(d => d.VoucherSan)
                .Include(d => d.VoucherHeThong)
                .Include(d => d.StaffCheckIn)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (don == null) return NotFound();
            return View(don);
        }

        // Admin: Danh sach tat ca hop dong + filter
        public async Task<IActionResult> TatCaHopDong(string? tuKhoa, string? sapXep)
        {
            var query = _context.SanBongs
                .Include(s => s.Owner)
                .Where(s => s.DaKyHopDong && !string.IsNullOrEmpty(s.NoiDungHopDong))
                .AsQueryable();

            if (!string.IsNullOrEmpty(tuKhoa))
                query = query.Where(s => s.TenSan.Contains(tuKhoa)
                                      || s.Owner.HoTen.Contains(tuKhoa)
                                      || s.Quan.Contains(tuKhoa));

            query = sapXep == "cu_nhat"
                ? query.OrderBy(s => s.NgayKyHopDong)
                : query.OrderByDescending(s => s.NgayKyHopDong);

            var list = await query.Take(100).ToListAsync();
            ViewBag.TuKhoa = tuKhoa;
            ViewBag.SapXep = sapXep;
            ViewBag.TongHD = list.Count;
            return View(list);
        }

        // Admin: Xuat Word (RTF) hop dong
        public async Task<IActionResult> XuatWordHopDong(int sanId)
        {
            var san = await _context.SanBongs
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == sanId);

            if (san == null || string.IsNullOrEmpty(san.NoiDungHopDong))
                return NotFound();

            var tenFile = $"HopDong_{san.TenSan.Replace(" ", "_")}_{san.NgayKyHopDong:yyyyMMdd}.rtf";
            var bytes = TaoRtfHopDong(san, san.NoiDungHopDong);
            return File(bytes, "application/rtf", tenFile);
        }

        private static byte[] TaoRtfHopDong(Web_Stadium.EFCore.SanBong san, string noiDung)
        {
            // RTF header
            var header = new System.Text.StringBuilder();
            header.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\deflang1066");
            header.AppendLine(@"{\fonttbl{\f0\froman\fprq2\fcharset0 Times New Roman;}}");
            header.AppendLine(@"{\colortbl ;\red0\green128\blue0;\red100\green100\blue100;}");
            header.AppendLine(@"\paperw11906\paperh16838");
            header.AppendLine(@"\margl1800\margr1800\margt1440\margb1440");
            header.AppendLine(@"\widowctrl\hyphauto");

            var body = new System.Text.StringBuilder();

            // Phan noi dung hop dong chinh (tu NoiDungHopDong)
            var lines = noiDung.Replace("\r", "").Split('\n');
            foreach (var raw in lines)
            {
                var l = raw.TrimEnd();
                if (string.IsNullOrWhiteSpace(l))
                { body.AppendLine(@"\pard\par"); continue; }

                var esc = RtfEsc(l);

                // Bo qua duong ke ngang (───)
                if (l.Contains("\u2500\u2500\u2500") || l.Contains("───"))
                    continue;

                // Quoc hieu, doc lap
                if (l.StartsWith("C\u1ed8NG HO\u00c0") || l.StartsWith("C\u1ed8NG H\u00d2A") ||
                    l.StartsWith("\u0110\u1ed9c l\u1eadp"))
                    body.AppendLine($@"\pard\qc\f0\fs24\b {esc}\b0\par");
                // Tieu de hop dong lon
                else if (l.Contains("H\u1ee2P \u0110\u1ed2NG H\u1ee2P T\u00c1C") || l.StartsWith("S\u1ed1:"))
                    body.AppendLine($@"\pard\qc\f0\fs28\b {esc}\b0\par");
                // Dieu khoan (Dieu 1. Dieu 2. ...)
                else if (System.Text.RegularExpressions.Regex.IsMatch(l.TrimStart(), @"^\u0110i\u1ec1u [0-9]+\."))
                    body.AppendLine($@"\pard\ql\sb200\f0\fs24\b {esc}\b0\par");
                // Tieu muc (1.1. 2.3. ...)
                else if (System.Text.RegularExpressions.Regex.IsMatch(l.TrimStart(), @"^[0-9]+\.[0-9]+\."))
                    body.AppendLine($@"\pard\ql\sb100\f0\fs22\b {esc}\b0\par");
                // Dong gach dau
                else if (l.TrimStart().StartsWith("  -") || l.TrimStart().StartsWith("- ") ||
                         l.TrimStart().StartsWith("   -"))
                    body.AppendLine($@"\pard\ql\li720\fi-360\f0\fs22 {esc}\par");
                // Phan chu ky / dia diem ngay thang
                else if (l.TrimStart().StartsWith("H\u00e0 N\u1ed9i") || l.TrimStart().StartsWith("Th\u00e0nh ph\u1ed1") ||
                         l.Contains("\u0110\u1ea0I DI\u1ec6N B\u00caNA") || l.Contains("\u0110\u1ea0I DI\u1ec6N B\u00caN B") ||
                         l.Contains("\u0110\u1ea0I DI\u1ec6N B\u1ec2N A") || l.Contains("\u0110\u1ea0I DI\u1ec6N B\u1ec2N B") ||
                         l.Contains("(K\u00fd,") || l.Contains("C\u00f4ng ty TNHH") || l.Contains("Gi\u00e1m \u0111\u1ed1c") ||
                         l.Contains("Ch\u1ee7 s\u00e2n"))
                    body.AppendLine($@"\pard\ql\f0\fs22 {esc}\par");
                else
                    body.AppendLine($@"\pard\ql\f0\fs22 {esc}\par");
            }

            // Phan chu ky cuoi hop dong - them tu dong neu chua co trong noiDung
            if (!noiDung.Contains("\u0110\u1ea0I DI\u1ec6N B\u00caNA") && !noiDung.Contains("\u0110\u1ea0I DI\u1ec6N B\u1ec2N A"))
            {
                var ngayKy = san.NgayKyHopDong?.ToString("dd/MM/yyyy") ?? System.DateTime.Now.ToString("dd/MM/yyyy");
                var tenOwner = RtfEsc(san.Owner?.HoTen ?? "");
                var tenSan = RtfEsc(san.TenSan ?? "");

                body.AppendLine(@"\pard\par");
                body.AppendLine($@"\pard\ql\f0\fs22                 {RtfEsc("H\u00e0 N\u1ed9i, ng\u00e0y")} {RtfEsc(ngayKy.Split('/')[0])} {RtfEsc("th\u00e1ng")} {RtfEsc(ngayKy.Split('/')[1])} {RtfEsc("n\u0103m")} {RtfEsc(ngayKy.Split('/')[2])}\par");
                body.AppendLine(@"\pard\par");
                body.AppendLine($@"\pard\ql\f0\fs22         {RtfEsc("\u0110\u1ea0I DI\u1ec6N B\u00caN A")}                    {RtfEsc("\u0110\u1ea0I DI\u1ec6N B\u00caN B")}\par");
                body.AppendLine($@"\pard\ql\f0\fs22    {RtfEsc("(K\u00fd, ghi r\u00f5 h\u1ecd t\u00ean, \u0111\u00f3ng d\u1ea5u)")}      {RtfEsc("(K\u00fd, ghi r\u00f5 h\u1ecd t\u00ean)")}\par");
                body.AppendLine(@"\pard\par");
                body.AppendLine(@"\pard\par");
                body.AppendLine(@"\pard\par");
                body.AppendLine($@"\pard\ql\f0\fs22    {RtfEsc("C\u00f4ng ty TNHH PitchHub")}                  {tenOwner}\par");
                body.AppendLine($@"\pard\ql\f0\fs22    {RtfEsc("Gi\u00e1m \u0111\u1ed1c \u0111i\u1ec1u h\u00e0nh")}                     {RtfEsc("Ch\u1ee7 s\u00e2n")} {tenSan}\par");
            }

            var rtf = header.ToString() + body.ToString() + "}";
            return System.Text.Encoding.GetEncoding(1252).GetBytes(rtf);
        }

        private static string RtfEsc(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s)
            {
                if (ch == '\\') sb.Append(@"\\");
                else if (ch == '{') sb.Append(@"\{");
                else if (ch == '}') sb.Append(@"\}");
                else if (ch > 127) sb.Append("\\u" + (int)ch + "?");
                else sb.Append(ch);
            }
            return sb.ToString();
        }


        public async Task<IActionResult> QuanLyUser(string? vaiTro, string? tuKhoa, string? quan, int? ownerId)
        {
            var query = _context.Users
                .Include(u => u.SanBongs)
                .Include(u => u.StaffSanPhanCongs)
                    .ThenInclude(s => s.SanBong)
                .AsQueryable();

            if (!string.IsNullOrEmpty(vaiTro))
                query = query.Where(u => u.VaiTro == vaiTro);

            if (!string.IsNullOrEmpty(tuKhoa))
                query = query.Where(u => u.HoTen.Contains(tuKhoa) || u.Email.Contains(tuKhoa));

            // Lọc theo quận của sân (dành cho Staff và Owner)
            if (!string.IsNullOrEmpty(quan))
                query = query.Where(u =>
                    u.SanBongs.Any(s => s.Quan == quan) ||                          // Owner có sân thuộc quận
                    u.StaffSanPhanCongs.Any(s => s.SanBong.Quan == quan));          // Staff được gán sân thuộc quận

            // Lọc Staff thuộc Owner cụ thể
            if (ownerId.HasValue)
                query = query.Where(u =>
                    u.OwnerIdCuaStaff == ownerId.Value ||
                    u.Id == ownerId.Value);

            ViewBag.VaiTro = vaiTro;
            ViewBag.TuKhoa = tuKhoa;
            ViewBag.Quan = quan;
            ViewBag.OwnerId = ownerId;

            // Dropdown quận
            ViewBag.QuanList = await _context.DanhMucQuans
                .Where(q => q.IsActive)
                .OrderBy(q => q.ThuTu)
                .ToListAsync();

            // Dropdown Owner (để lọc Staff)
            ViewBag.OwnerList = await _context.Users
                .Where(u => u.VaiTro == "Owner")
                .OrderBy(u => u.HoTen)
                .ToListAsync();

            return View(await query.OrderByDescending(u => u.NgayTao).Take(200).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiTaiKhoan(int id, bool isActive)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (user.Id == GetAdminId())
            { TempData["Error"] = "Không thể khoá tài khoản đang đăng nhập!"; return RedirectToAction("QuanLyUser"); }
            if (user.VaiTro == "Admin")
            { TempData["Error"] = "Không thể khoá tài khoản Admin!"; return RedirectToAction("QuanLyUser"); }
            user.IsActive = isActive;
            await _context.SaveChangesAsync();
            await GhiLog(isActive ? "MoKhoaTaiKhoan" : "KhoaTaiKhoan", "User", id,
                $"{(isActive ? "Mở" : "Khoá")} TK: {user.HoTen} ({user.VaiTro})");
            TempData["Success"] = $"{(isActive ? "Mở khoá" : "Khoá")} \"{user.HoTen}\"!";
            return RedirectToAction("QuanLyUser");
        }

        [HttpPost]
        public async Task<IActionResult> DoiVaiTro(int id, string vaiTro)
        {
            var dsVaiTroHopLe = new[] { "User", "Owner", "Staff" };
            if (!dsVaiTroHopLe.Contains(vaiTro))
            { TempData["Error"] = "Vai trò không hợp lệ!"; return RedirectToAction("QuanLyUser"); }

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (user.VaiTro == "Admin")
            { TempData["Error"] = "Không thể đổi vai trò Admin!"; return RedirectToAction("QuanLyUser"); }
            if (user.Id == GetAdminId())
            { TempData["Error"] = "Không thể đổi vai trò tài khoản đang đăng nhập!"; return RedirectToAction("QuanLyUser"); }

            var oldVaiTro = user.VaiTro;
            user.VaiTro = vaiTro;
            // Nếu đổi sang không phải Staff thì xóa OwnerIdCuaStaff
            if (vaiTro != "Staff") user.OwnerIdCuaStaff = null;
            await _context.SaveChangesAsync();
            await GhiLog("DoiVaiTro", "User", id, $"{user.HoTen}: {oldVaiTro} → {vaiTro}");
            TempData["Success"] = $"Đã đổi vai trò \"{user.HoTen}\": {oldVaiTro} → {vaiTro}";
            return RedirectToAction("QuanLyUser");
        }
        // ══════════════════════════════════════════════════════════
        // KHIẾU NẠI & HOÀN CỌC
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> KhieuNai(string? trangThai)
        {
            trangThai ??= "ChoXuLy";
            var list = await _context.KhieuNais
                .Include(k => k.User)
                .Include(k => k.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(k => k.AdminXuLy)
                .Where(k => trangThai == "TatCa" || k.TrangThai == trangThai)
                .OrderByDescending(k => k.NgayGui).ToListAsync();
            ViewBag.TrangThaiFilter = trangThai;
            ViewBag.SoChoXuLy = await _context.KhieuNais.CountAsync(k => k.TrangThai == "ChoXuLy");
            ViewBag.KhieuNais = list;
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> XuLyKhieuNai(int id, string ketQua, decimal? soTienHoan, string? ghiChu)
        {
            var kn = await _context.KhieuNais
                .Include(k => k.DatSan).Include(k => k.User)
                .FirstOrDefaultAsync(k => k.Id == id);
            if (kn == null) return NotFound();
            kn.TrangThai = ketQua;
            kn.SoTienHoan = soTienHoan;
            kn.GhiChuAdmin = ghiChu;
            kn.NgayXuLy = DateTime.Now;
            kn.AdminXuLyId = GetAdminId();
            if (ketQua == "DaHoanCoc" && kn.DatSan != null) kn.DatSan.TrangThai = "DaHuy";
            await _context.SaveChangesAsync();
            await GhiLog(ketQua == "DaHoanCoc" ? "HoanCoc" : "TuChoiHoanCoc",
                "KhieuNai", id, $"{kn.User?.HoTen} — {soTienHoan:N0}đ");

            // Gửi email thông báo kết quả cho User
            if (kn.User != null && !string.IsNullOrEmpty(kn.User.Email))
            {
                if (ketQua == "DaHoanCoc")
                {
                    await _emailService.GuiEmailAsync(
                        kn.User.Email, kn.User.HoTen,
                        "✅ Khiếu nại của bạn đã được chấp nhận — PitchHub",
                        $@"<div style='font-family:Arial,sans-serif;max-width:560px;margin:0 auto;padding:28px;'>
                            <h2 style='color:#1ed760;'>Khiếu nại được chấp nhận</h2>
                            <p>Xin chào <strong>{kn.User.HoTen}</strong>,</p>
                            <p>Admin PitchHub đã xem xét và chấp nhận khiếu nại của bạn.</p>
                            <div style='background:#f8fffe;border:1px solid #1ed760;border-radius:10px;padding:16px;'>
                                <p>💰 Số tiền hoàn: <strong style='color:#1ed760;font-size:1.3rem;'>{soTienHoan:N0}đ</strong></p>
                                <p>📝 Ghi chú: {ghiChu ?? "Hoàn tiền theo chính sách PitchHub"}</p>
                                <p>⏱ Tiền hoàn về phương thức thanh toán ban đầu trong 1-3 ngày làm việc.</p>
                            </div>
                        </div>"
                    );
                }
                else
                {
                    await _emailService.GuiEmailAsync(
                        kn.User.Email, kn.User.HoTen,
                        "❌ Khiếu nại của bạn không được chấp nhận — PitchHub",
                        $@"<div style='font-family:Arial,sans-serif;max-width:560px;margin:0 auto;padding:28px;'>
                            <h2 style='color:#e74c3c;'>Khiếu nại không được chấp nhận</h2>
                            <p>Xin chào <strong>{kn.User.HoTen}</strong>,</p>
                            <p>Sau khi xem xét, Admin PitchHub không thể chấp nhận khiếu nại này.</p>
                            <div style='background:#fff8f0;border:1px solid #f59e0b;border-radius:10px;padding:16px;'>
                                <p>📝 Lý do: {ghiChu ?? "Khiếu nại không có cơ sở theo chính sách PitchHub"}</p>
                            </div>
                            <p>Nếu cần hỗ trợ thêm, vui lòng liên hệ support@pitchhub.vn</p>
                        </div>"
                    );
                }
            }

            TempData["Success"] = ketQua == "DaHoanCoc"
                ? $"Đã hoàn {soTienHoan:N0}đ cho \"{kn.User?.HoTen}\" — Email đã gửi"
                : "Đã từ chối khiếu nại — Email đã gửi cho khách.";
            return RedirectToAction("KhieuNai");
        }

        // ══════════════════════════════════════════════════════════
        // BÁO CÁO — KA/KB + xếp hạng sân theo hoa hồng
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> BaoCao(
            string loai = "thang", int? nam = null, int? thang = null,
            int? ownerId = null, int? sanId = null)
        {
            var now = DateTime.Now;
            nam ??= now.Year;
            thang ??= now.Month;

            ViewBag.Loai = loai; ViewBag.Nam = nam; ViewBag.Thang = thang;
            ViewBag.OwnerId = ownerId; ViewBag.SanId = sanId;

            DateTime batDau, ketThuc;
            var data = new List<object>();
            string tieuDe;

            // Load tất cả đơn kèm SanBong (cần TyLeHoaHong)
            IQueryable<DatSan> baseQ = _context.DatSans
                .AsNoTracking()
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong).ThenInclude(s => s.Owner)
                .Where(d => d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                         || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy");

            // Filter theo chủ sân / sân cụ thể
            if (ownerId.HasValue)
                baseQ = baseQ.Where(d => d.KhungGio.SanBong.OwnerId == ownerId.Value);
            if (sanId.HasValue)
                baseQ = baseQ.Where(d => d.KhungGio.SanBongId == sanId.Value);

            // Load danh sách owner và sân cho dropdown
            ViewBag.OwnerList = await _context.Users
                .Where(u => u.VaiTro == "Owner")
                .OrderBy(u => u.HoTen)
                .Select(u => new { u.Id, u.HoTen })
                .ToListAsync();
            ViewBag.SanList = ownerId.HasValue
                ? await _context.SanBongs
                    .Where(s => s.OwnerId == ownerId.Value && s.TrangThaiDuyet == "DaDuyet")
                    .OrderBy(s => s.TenSan).Select(s => new { s.Id, s.TenSan }).ToListAsync()
                : await _context.SanBongs
                    .Where(s => s.TrangThaiDuyet == "DaDuyet")
                    .OrderBy(s => s.TenSan).Select(s => new { s.Id, s.TenSan }).ToListAsync();

            // Pre-load tyLeMap cho BaoCao
            var tyLeMapBaoCao = await _context.DanhMucQuans
                .AsNoTracking()
                .Include(q => q.VungKhuVuc)
                .Where(q => q.VungKhuVuc != null)
                .ToDictionaryAsync(q => q.TenQuan, q => q.VungKhuVuc!.TyLeHoaHong);

            switch (loai)
            {
                case "ngay":
                    batDau = new DateTime(nam.Value, thang.Value, 1);
                    ketThuc = batDau.AddMonths(1);
                    tieuDe = $"Theo ngày — Tháng {thang}/{nam}";
                    // Load toàn bộ tháng một lần thay vì N query theo ngày
                    var allRowsNgay = await baseQ.Where(d => d.ThoiGianTao >= batDau && d.ThoiGianTao < ketThuc).ToListAsync();
                    for (int ng = 1; ng <= DateTime.DaysInMonth(nam.Value, thang.Value); ng++)
                    {
                        var bd = new DateTime(nam.Value, thang.Value, ng);
                        var kt = bd.AddDays(1);
                        var rows = allRowsNgay.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToList();
                        data.Add(BuildDataPoint($"{ng}/{thang}", rows, tyLeMapBaoCao));
                    }
                    break;

                case "tuan":
                    batDau = new DateTime(nam.Value, thang.Value, 1);
                    ketThuc = batDau.AddMonths(1);
                    tieuDe = $"Theo tuần — Tháng {thang}/{nam}";
                    var allRowsTuan = await baseQ.Where(d => d.ThoiGianTao >= batDau && d.ThoiGianTao < ketThuc).ToListAsync();
                    int tuan = 1; var cur = batDau;
                    while (cur < ketThuc)
                    {
                        var kt = cur.AddDays(7) < ketThuc ? cur.AddDays(7) : ketThuc;
                        var rows = allRowsTuan.Where(d => d.ThoiGianTao >= cur && d.ThoiGianTao < kt).ToList();
                        data.Add(BuildDataPoint($"T{tuan} ({cur:dd/MM}–{kt.AddDays(-1):dd/MM})", rows, tyLeMapBaoCao));
                        cur = kt; tuan++;
                    }
                    break;

                case "quy":
                    batDau = new DateTime(nam.Value, 1, 1);
                    ketThuc = new DateTime(nam.Value + 1, 1, 1);
                    tieuDe = $"Theo quý — Năm {nam}";
                    var allRowsQuy = await baseQ.Where(d => d.ThoiGianTao >= batDau && d.ThoiGianTao < ketThuc).ToListAsync();
                    for (int q = 1; q <= 4; q++)
                    {
                        var bd = new DateTime(nam.Value, (q - 1) * 3 + 1, 1);
                        var kt = bd.AddMonths(3);
                        var rows = allRowsQuy.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToList();
                        data.Add(BuildDataPoint($"Q{q}/{nam}", rows, tyLeMapBaoCao));
                    }
                    break;

                case "nam":
                    batDau = new DateTime(now.Year - 4, 1, 1);
                    ketThuc = new DateTime(now.Year + 1, 1, 1);
                    tieuDe = "Theo năm — 5 năm gần nhất";
                    var allRowsNam = await baseQ.Where(d => d.ThoiGianTao >= batDau && d.ThoiGianTao < ketThuc).ToListAsync();
                    for (int y = now.Year - 4; y <= now.Year; y++)
                    {
                        var bd = new DateTime(y, 1, 1);
                        var kt = new DateTime(y + 1, 1, 1);
                        var rows = allRowsNam.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToList();
                        data.Add(BuildDataPoint($"{y}", rows, tyLeMapBaoCao));
                    }
                    break;

                default: // thang
                    batDau = new DateTime(nam.Value, 1, 1);
                    ketThuc = new DateTime(nam.Value + 1, 1, 1);
                    tieuDe = $"Theo tháng — Năm {nam}";
                    var allRowsThang = await baseQ.Where(d => d.ThoiGianTao >= batDau && d.ThoiGianTao < ketThuc).ToListAsync();
                    for (int t = 1; t <= 12; t++)
                    {
                        var bd = new DateTime(nam.Value, t, 1);
                        var kt = bd.AddMonths(1);
                        var rows = allRowsThang.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToList();
                        data.Add(BuildDataPoint($"T{t}", rows, tyLeMapBaoCao));
                    }
                    break;
            }

            ViewBag.Data = data; ViewBag.TieuDe = tieuDe;
            ViewBag.TongPhiHoaHong = (double)data.Sum(d => (double)((dynamic)d).phi);
            ViewBag.TongDoanhThuSan = (double)data.Sum(d => (double)((dynamic)d).dtSan);
            ViewBag.TongLuot = (int)data.Sum(d => (int)((dynamic)d).soLuot);
            ViewBag.DiemCaoNhat = data.OrderByDescending(d => (double)((dynamic)d).phi).FirstOrDefault();

            // Aliases cho BaoCao View
            ViewBag.DoanhThuNam = data.Select(d => new
            {
                thang = (string)((dynamic)d).nhan,
                dt = (double)((dynamic)d).dtSan,
                soLuot = (int)((dynamic)d).soLuot
            }).Cast<dynamic>().ToList();

            ViewBag.DoanhThuTheoOwner = ViewBag.XepHangSan; // duoc set ben duoi


            // Xếp hạng sân theo hoa hồng — reuse tyLeMapBaoCao đã load
            var allRows = await baseQ
                .Where(d => d.ThoiGianTao >= batDau && d.ThoiGianTao < ketThuc)
                .ToListAsync();

            ViewBag.XepHangSan = allRows
                .GroupBy(d => new
                {
                    TenSan = d.KhungGio?.SanBong?.TenSan ?? "?",
                    Owner = d.KhungGio?.SanBong?.Owner?.HoTen ?? "?",
                    TyLe = tyLeMapBaoCao.TryGetValue(
                                 d.KhungGio?.SanBong?.Quan ?? "",
                                 out var tl) ? tl : 0.10m
                })
                .Select(g => new
                {
                    TenSan = g.Key.TenSan,
                    Owner = g.Key.Owner,
                    TyLeHH = (double)g.Key.TyLe,
                    TongDT = g.Sum(d => (double)TinhDoanhThuPhaSinh(d)),
                    PhiHoaHong = g.Sum(d => (double)TinhPhiHoaHong(d)),
                    SoLuot = g.Count(),
                    SoLuotKA = g.Count(d => d.TrangThai == "DaHuy"),
                    SoLuotKB = g.Count(d => d.TrangThai == "HoanThanh"
                                            || d.TrangThai == "DangSuDung")
                })
                .OrderByDescending(x => x.PhiHoaHong)
                .ToList();

            ViewBag.TyLeLapDay = await _context.SanBongs
                .Include(s => s.KhungGios)
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .Select(s => new
                {
                    Ten = s.TenSan,
                    Tong = s.KhungGios.Count,
                    DaDat = s.KhungGios.Count(k => k.TrangThai == "DaDat")
                }).ToListAsync();
            ViewBag.TyLe = ViewBag.TyLeLapDay;
            ViewBag.DoanhThuTheoOwner = ViewBag.XepHangSan;

            return View();
        }

        private object BuildDataPoint(string nhan, List<DatSan> rows, Dictionary<string, decimal>? tyLeMap = null) => new
        {
            nhan = nhan,
            dtSan = (double)rows.Sum(d => TinhDoanhThuPhaSinh(d)),
            phi = (double)rows.Sum(d => TinhPhiHoaHong(d, tyLeMap)),
            soLuot = rows.Count,
            soKA = rows.Count(d => d.TrangThai == "DaHuy"),
            soKB = rows.Count(d => d.TrangThai == "HoanThanh" || d.TrangThai == "DangSuDung")
        };

        // ══════════════════════════════════════════════════════════
        // AUDIT LOG
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> AuditLog(
            string? hanhDong, int? userId,
            DateTime? tuNgay, DateTime? denNgay, int trang = 1)
        {
            var query = _context.AuditLogs.Include(a => a.User).AsQueryable();
            if (!string.IsNullOrEmpty(hanhDong)) query = query.Where(a => a.HanhDong == hanhDong);
            if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
            if (tuNgay.HasValue) query = query.Where(a => a.ThoiGian >= tuNgay.Value);
            if (denNgay.HasValue) query = query.Where(a => a.ThoiGian < denNgay.Value.AddDays(1));

            const int pageSize = 20;
            ViewBag.TongSo = await query.CountAsync();
            ViewBag.Trang = trang;
            ViewBag.TongTrang = (int)Math.Ceiling((double)ViewBag.TongSo / pageSize);
            ViewBag.HanhDong = hanhDong; ViewBag.UserId = userId;
            ViewBag.TuNgay = tuNgay?.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = denNgay?.ToString("yyyy-MM-dd");
            ViewBag.DanhSachHanhDong = await _context.AuditLogs
                .Select(a => a.HanhDong).Distinct().OrderBy(h => h).ToListAsync();

            return View(await query.OrderByDescending(a => a.ThoiGian)
                .Skip((trang - 1) * pageSize).Take(pageSize).ToListAsync());
        }

        // AJAX: Số khiếu nại chờ xử lý — dùng cho badge navbar
        [HttpGet]
        public async Task<IActionResult> GetSoKhieuNai()
        {
            var so = await _context.KhieuNais.CountAsync(k => k.TrangThai == "ChoXuLy");
            var soSan = await _context.SanBongs.CountAsync(s => s.TrangThaiDuyet == "ChoDuyet");
            return Json(new { soKhieuNai = so, soSanChoDuyet = soSan });
        }

        // ══════════════════════════════════════════════════════════
        // HO SO
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> HoSo(string tab = "suckhoe")
        {
            var adminId = TokenHelper.LayUserId(Request, _config);
            if (adminId == null) return RedirectToAction("Login", "Auth");
            var user = await _context.Users.FindAsync(adminId);

            // KPI hôm nay
            var homNay = DateTime.Today;
            var donHomNay = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => d.ThoiGianTao.Date == homNay
                         && (d.TrangThai == "HoanThanh" || d.TrangThai == "DaXacNhan"))
                .ToListAsync();

            double dtHomNay = (double)donHomNay.Sum(d => d.TongTien > 0 ? d.TongTien : d.TienCoc);

            // Cảnh báo hệ thống
            var canhBao = new List<string>();
            int knCho = await _context.KhieuNais.CountAsync(k => k.TrangThai == "ChoXuLy");
            int sanCho = await _context.SanBongs.CountAsync(s => s.TrangThaiDuyet == "ChoDuyet");
            if (knCho > 0) canhBao.Add($"{knCho} khiếu nại đang chờ xử lý");
            if (sanCho > 0) canhBao.Add($"{sanCho} sân mới đang chờ phê duyệt");

            ViewBag.TongSan = await _context.SanBongs.CountAsync(s => s.TrangThaiDuyet == "DaDuyet");
            ViewBag.TongUser = await _context.Users.CountAsync(u => u.VaiTro == "User");
            ViewBag.TongOwner = await _context.Users.CountAsync(u => u.VaiTro == "Owner");
            ViewBag.TongStaff = await _context.Users.CountAsync(u => u.VaiTro == "Staff");
            ViewBag.TongAdmin = await _context.Users.CountAsync(u => u.VaiTro == "Admin");
            ViewBag.TongKN = knCho;
            ViewBag.SanChoDuyet = sanCho;
            ViewBag.SanDaDuyet = await _context.SanBongs.CountAsync(s => s.TrangThaiDuyet == "DaDuyet");
            ViewBag.SanTuChoi = await _context.SanBongs.CountAsync(s => s.TrangThaiDuyet == "TuChoi");
            ViewBag.DoanhThuHomNay = dtHomNay;
            ViewBag.LuotDatHomNay = donHomNay.Count;
            ViewBag.UserMoiTuan = await _context.Users
                .CountAsync(u => u.NgayTao >= DateTime.Now.AddDays(-7));
            ViewBag.CanhBao = canhBao;
            ViewBag.AuditLogs = await _context.AuditLogs
                .Where(a => a.UserId == adminId)
                .OrderByDescending(a => a.ThoiGian).Take(50).ToListAsync();
            ViewBag.Tab = tab;
            return View(user);
        }
        // ══════════════════════════════════════════════════════════
        // QUAN LY SAN
        // ══════════════════════════════════════════════════════════

        public async Task<IActionResult> QuanLySan(
    int? ownerId, string? trangThai, string? keyword)
        {
            // Danh sách Owner để filter dropdown
            ViewBag.OwnerList = await _context.Users
                .Where(u => u.VaiTro == "Owner")
                .OrderBy(u => u.HoTen)
                .ToListAsync();

            // Query sân — include Owner, DichVus, AnhSanBongs, Quan→Vung(Lat/Lng)
            var query = _context.SanBongs
                .Include(s => s.Owner)
                .Include(s => s.DichVus)
                .Include(s => s.AnhSanBongs)
                .AsQueryable();

            // Lọc theo Owner
            if (ownerId.HasValue)
                query = query.Where(s => s.OwnerId == ownerId.Value);

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(s => s.TrangThaiDuyet == trangThai);

            // Tìm theo tên hoặc địa chỉ
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(s =>
                    s.TenSan.Contains(keyword) ||
                    s.DiaChi.Contains(keyword) ||
                    s.Quan.Contains(keyword));

            var sanList = await query.OrderBy(s => s.TenSan).ToListAsync();

            // Lấy tọa độ theo quận (Lat/Lng nằm ở VungKhuVuc qua DanhMucQuan)
            var quanMap = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.IsActive)
                .ToDictionaryAsync(
                    q => q.TenQuan,
                    q => new { lat = q.VungKhuVuc?.Lat ?? 21.028, lng = q.VungKhuVuc?.Lng ?? 105.854 }
                );

            // Build data cho bản đồ — gán tọa độ từ vùng của quận sân đó
            var mapData = sanList.Select(s => new {
                id = s.Id,
                ten = s.TenSan,
                diaChi = s.DiaChi + ", " + s.Quan,
                trangThai = s.TrangThaiDuyet,
                owner = s.Owner?.HoTen ?? "",
                loaiSan = s.LoaiSan,
                lat = s.Latitude != 0 ? s.Latitude : (quanMap.ContainsKey(s.Quan ?? "") ? quanMap[s.Quan!].lat : 21.028),
                lng = s.Longitude != 0 ? s.Longitude : (quanMap.ContainsKey(s.Quan ?? "") ? quanMap[s.Quan!].lng : 105.854),
                dichVus = (s.DichVus ?? new List<DichVu>())
                                .Where(d => d.IsActive)
                                .Select(d => new { ten = d.TenDichVu, gia = d.Gia, kho = d.TonKho })
                                .ToList()
            }).ToList();

            ViewBag.SanList = sanList;
            ViewBag.MapDataJson = System.Text.Json.JsonSerializer.Serialize(mapData);
            ViewBag.FilterOwner = ownerId;
            ViewBag.FilterTT = trangThai;
            ViewBag.Keyword = keyword;

            return View();
        }

        // ══════════════════════════════════════════════════════════
        // GIÁM SÁT YÊU CẦU ĐỔI GIỜ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> YeuCauDoiGio(string? trangThai)
        {
            var query = _context.YeuCauDoiGios
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.KhungGioMoi)
                .Include(y => y.StaffXuLy)
                .Include(y => y.OwnerXuLy)
                .AsQueryable();

            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(y => y.TrangThai == trangThai);

            var list = await query.OrderByDescending(y => y.ThoiGianTao).Take(100).ToListAsync();
            ViewBag.FilterTT = trangThai;
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> GhiDeYeuCau(int yeuCauId, string hanhDong, string? ghiChuAdmin)
        {
            var adminId = GetAdminId();
            var yc = await _context.YeuCauDoiGios
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.KhungGioMoi)
                .FirstOrDefaultAsync(y => y.Id == yeuCauId);

            if (yc == null) return NotFound();

            yc.AdminXuLyId = adminId;
            yc.GhiChuAdmin = ghiChuAdmin?.Trim();
            yc.ThoiGianXuLy = DateTime.Now;

            if (hanhDong == "PheDuyet" && yc.TrangThai != "DaPheDuyet")
            {
                var kgMoi = await _context.KhungGios.FindAsync(yc.KhungGioMoiId);
                if (kgMoi != null && kgMoi.TrangThai == "Trong")
                {
                    var kgCu = yc.DatSan.KhungGio;
                    kgCu.TrangThai = "Trong";
                    kgCu.ThoiGianHetGiuCho = null;
                    kgMoi.TrangThai = "DaDat";
                    yc.DatSan.KhungGioId = yc.KhungGioMoiId;
                    yc.DatSan.NgayThiDau = yc.NgayMoi;
                }
                yc.TrangThai = "DaPheDuyet";
                await GhiLog("AdminPheDuyetDoiGio", "YeuCauDoiGio", yc.Id, $"Admin ghi đè phê duyệt đổi giờ đơn {yc.DatSan.MaXacNhan}");
                TempData["Success"] = "Admin đã ghi đè — phê duyệt đổi giờ thành công.";
            }
            else if (hanhDong == "TuChoi" && yc.TrangThai != "TuChoi")
            {
                yc.TrangThai = "TuChoi";
                await GhiLog("AdminTuChoiDoiGio", "YeuCauDoiGio", yc.Id, $"Admin ghi đè từ chối đổi giờ đơn {yc.DatSan.MaXacNhan}");
                TempData["Success"] = "Admin đã ghi đè — từ chối yêu cầu đổi giờ.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("YeuCauDoiGio");
        }

        // ══════════════════════════════════════════════════════════
        // GIÁM SÁT YÊU CẦU ĐỔI SÂN
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> YeuCauDoiSan(string? trangThai)
        {
            var query = _context.YeuCauDoiSans
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.SanMoi)
                .Include(y => y.KhungGioMoi)
                .Include(y => y.OwnerXuLy)
                .AsQueryable();

            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(y => y.TrangThai == trangThai);

            ViewBag.TrangThaiFilter = trangThai;
            return View(await query.OrderByDescending(y => y.ThoiGianTao).Take(100).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> GhiDeDoiSan(int yeuCauId, string hanhDong, string? ghiChuAdmin)
        {
            var adminId = GetAdminId();
            var yc = await _context.YeuCauDoiSans
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.SanMoi)
                .Include(y => y.KhungGioMoi)
                .FirstOrDefaultAsync(y => y.Id == yeuCauId);

            if (yc == null) return NotFound();

            if (hanhDong == "PheDuyet" && yc.TrangThai != "DaPheDuyet")
            {
                var kgMoi = await _context.KhungGios.FindAsync(yc.KhungGioMoiId);
                if (kgMoi != null && kgMoi.TrangThai == "Trong")
                {
                    var don = yc.DatSan;
                    var kgCu = don.KhungGio;
                    if (kgCu != null) { kgCu.TrangThai = "Trong"; kgCu.ThoiGianHetGiuCho = null; }
                    kgMoi.TrangThai = "DaDat";
                    don.KhungGioId = yc.KhungGioMoiId;
                }
                yc.TrangThai = "DaPheDuyet";
                yc.OwnerXuLyId = adminId;
                await GhiLog("AdminPheDuyetDoiSan", "YeuCauDoiSan", yc.Id, $"Admin ghi đè phê duyệt đổi sân đơn {yc.DatSan.MaXacNhan}");
                TempData["Success"] = "Admin đã ghi đè — phê duyệt đổi sân thành công.";
            }
            else if (hanhDong == "TuChoi" && yc.TrangThai != "TuChoi")
            {
                yc.TrangThai = "TuChoi";
                yc.OwnerXuLyId = adminId;
                await GhiLog("AdminTuChoiDoiSan", "YeuCauDoiSan", yc.Id, $"Admin ghi đè từ chối đổi sân đơn {yc.DatSan.MaXacNhan}");
                TempData["Success"] = "Admin đã ghi đè — từ chối yêu cầu đổi sân.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("YeuCauDoiSan");
        }

        // ══════════════════════════════════════════════════════════
        // GIÁM SÁT GHÉP TRẬN
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> GhepTran(string? trangThai)
        {
            var query = _context.GhepTrans
                .Include(g => g.DatSanA)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(g => g.DatSanA).ThenInclude(d => d.User)
                .Include(g => g.DatSanB)
                    .ThenInclude(d => d!.KhungGio).ThenInclude(k => k!.SanBong)
                .Include(g => g.DatSanB).ThenInclude(d => d!.User)
                .Include(g => g.UserA)
                .Include(g => g.UserB)
                .Include(g => g.SanChon)
                .AsQueryable();

            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(g => g.TrangThai == trangThai);

            ViewBag.TrangThaiFilter = trangThai;
            return View(await query.OrderByDescending(g => g.ThoiGianTao).Take(100).ToListAsync());
        }

        // ══════════════════════════════════════════════════════════
        // GET /Admin/ChuyenNhuong
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> ChuyenNhuong(string? trangThai)
        {
            var query = _context.ChuyenNhuongs
                .Include(c => c.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(c => c.UserA)
                .Include(c => c.UserB)
                .AsQueryable();

            if (!string.IsNullOrEmpty(trangThai))
                query = query.Where(c => c.TrangThai == trangThai);

            ViewBag.TrangThaiFilter = trangThai;
            return View(await query.OrderByDescending(c => c.ThoiGianTao).Take(100).ToListAsync());
        }

        // GET /Admin/XemHoSo/5
        public IActionResult XemHoSo(int id)
        {
            return RedirectToAction("XemHopDong", new { id });
        }

        // ══════════════════════════════════════════════════════════
        // VOUCHER HỆ THỐNG
        // ══════════════════════════════════════════════════════════

        // GET /Admin/Voucher
        public async Task<IActionResult> Voucher(string? filter = null)
        {
            var query = _context.Vouchers
                .Where(v => v.LoaiVoucher == "HeThong");

            if (filter == "active")
                query = query.Where(v => v.IsActive && v.NgayHetHan > DateTime.Now);
            else if (filter == "expired")
                query = query.Where(v => !v.IsActive || v.NgayHetHan <= DateTime.Now);

            var vouchers = await query.OrderByDescending(v => v.NgayTao).ToListAsync();

            var allHT = await _context.Vouchers.Where(v => v.LoaiVoucher == "HeThong").ToListAsync();
            ViewBag.TongPhatHanh = allHT.Sum(v => v.DaDung);
            ViewBag.TongConLai = allHT.Sum(v => v.SoLuong == 0 ? 0 : Math.Max(0, v.SoLuong - v.DaDung));
            ViewBag.TongTienGiam = await _context.DatSans
                .Where(d => d.VoucherHeThongId != null)
                .SumAsync(d => d.TienGiamHeThong);
            ViewBag.Filter = filter;

            return View(vouchers);
        }

        // GET /Admin/TaoVoucher
        public IActionResult TaoVoucher() => View();

        // POST /Admin/TaoVoucher
        [HttpPost]
        public async Task<IActionResult> TaoVoucher(
            string tenVoucher, string? moTa,
            string loaiGiam, decimal giaTriGiam, decimal? giamToiDa,
            decimal dieuKienToiThieu, int soLuong,
            DateTime ngayBatDau, DateTime ngayHetHan)
        {
            if (string.IsNullOrWhiteSpace(tenVoucher))
            { TempData["Error"] = "Tên voucher không được để trống."; return View(); }

            if (ngayHetHan <= ngayBatDau)
            { TempData["Error"] = "Ngày hết hạn phải sau ngày bắt đầu."; return View(); }

            if (giaTriGiam <= 0)
            { TempData["Error"] = "Giá trị giảm phải lớn hơn 0."; return View(); }

            var ma = $"HT-{DateTime.Now:yyyyMMddHHmmss}-{new Random().Next(100, 999)}";
            var v = new Web_Stadium.EFCore.Voucher
            {
                MaVoucher = ma,
                TenVoucher = tenVoucher.Trim(),
                MoTa = moTa?.Trim(),
                LoaiGiam = loaiGiam,
                GiaTriGiam = giaTriGiam,
                GiamToiDa = loaiGiam == "PhanTram" ? giamToiDa : null,
                DieuKienToiThieu = dieuKienToiThieu,
                SoLuong = soLuong,
                LoaiVoucher = "HeThong",
                NgayBatDau = ngayBatDau,
                NgayHetHan = ngayHetHan,
                IsActive = true,
                NgayTao = DateTime.Now,
                DiemCanDoi = 0,
                SoNgayHieuLuc = 0
            };
            _context.Vouchers.Add(v);
            await _context.SaveChangesAsync();
            await GhiLog("TaoVoucher", "Voucher", v.Id, $"Tạo voucher hệ thống: {tenVoucher}");
            TempData["Success"] = $"Đã tạo voucher \"{tenVoucher}\" — Mã: {ma}";
            return RedirectToAction("Voucher");
        }

        // POST /Admin/KichHoatVoucher/{id}
        [HttpPost]
        public async Task<IActionResult> KichHoatVoucher(int id)
        {
            var v = await _context.Vouchers.FindAsync(id);
            if (v == null || v.LoaiVoucher != "HeThong") return NotFound();
            v.IsActive = true;
            await _context.SaveChangesAsync();
            await GhiLog("KichHoatVoucher", "Voucher", id, $"Kích hoạt: {v.TenVoucher}");
            TempData["Success"] = $"Đã kích hoạt voucher \"{v.TenVoucher}\".";
            return RedirectToAction("Voucher");
        }

        // POST /Admin/VoHieuVoucher/{id}
        [HttpPost]
        public async Task<IActionResult> VoHieuVoucher(int id)
        {
            var v = await _context.Vouchers.FindAsync(id);
            if (v == null || v.LoaiVoucher != "HeThong") return NotFound();
            v.IsActive = false;
            await _context.SaveChangesAsync();
            await GhiLog("VoHieuVoucher", "Voucher", id, $"Vô hiệu: {v.TenVoucher}");
            TempData["Success"] = $"Đã vô hiệu voucher \"{v.TenVoucher}\".";
            return RedirectToAction("Voucher");
        }
    }
}