using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Owner")]
    public class OwnerController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HoanCocService _hoanCocService;

        public OwnerController(SanBongContext context, IConfiguration config, EmailService emailService, IHttpClientFactory httpClientFactory, HoanCocService hoanCocService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
            _httpClientFactory = httpClientFactory;
            _hoanCocService = hoanCocService;
        }
        // Helper lấy OwnerId từ JWT
        private int GetOwnerId()
        {
            var id = TokenHelper.LayUserId(Request, _config);
            if (id == null) throw new UnauthorizedAccessException("Token không hợp lệ hoặc đã hết hạn.");
            return id.Value;
        }

        // Helper: chỉ lấy sân thuộc Owner đang đăng nhập
        private IQueryable<SanBong> SanCuaToi() =>
            _context.SanBongs.Where(s => s.OwnerId == GetOwnerId());

        // Helper: load TyLeHoaHong map từ VungKhuVuc
        private async Task<Dictionary<string, decimal>> LayTyLeMapAsync()
        {
            return await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.VungKhuVuc != null)
                .ToDictionaryAsync(
                    q => q.TenQuan,
                    q => q.VungKhuVuc!.TyLeHoaHong
                );
        }

        // Helper: lấy tỷ lệ từ map theo tên quận
        private decimal LayTyLe(Dictionary<string, decimal> map, string? quan)
            => map.TryGetValue(quan ?? "", out var tl) ? tl : 0.10m;

        // ══════════════════════════════════════════════════════════
        // 1. DASHBOARD
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            var ownerId = GetOwnerId();
            var now = DateTime.Now;
            var thangNay = new DateTime(now.Year, now.Month, 1);

            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            ViewBag.TongSan = sanIds.Count;
            ViewBag.SanDaDuyet = await SanCuaToi()
                .CountAsync(s => s.TrangThaiDuyet == "DaDuyet");
            ViewBag.SanChoDuyet = await SanCuaToi()
                .CountAsync(s => s.TrangThaiDuyet == "ChoDuyet");

            // Đơn đặt sân hôm nay
            ViewBag.DonHomNay = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.NgayThiDau.Date == now.Date
                         && d.TrangThai != "DaHuy")
                .OrderBy(d => d.KhungGio.GioBatDau)
                .Take(10).ToListAsync();

            // Doanh thu tháng này (hoa hồng đã trừ)
            var datSansThang = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.ThoiGianTao >= thangNay
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                          || d.TrangThai == "DangSuDung"))
                .ToListAsync();

            var tyLeMap = await LayTyLeMapAsync();

            var tongDT = datSansThang.Sum(d => d.TongTien > 0 ? d.TongTien : d.TienCoc);
            var tongHH = datSansThang.Sum(d =>
            {
                var tyLe = LayTyLe(tyLeMap, d.KhungGio?.SanBong?.Quan);
                return (d.TongTien > 0 ? d.TongTien : d.TienCoc) * tyLe;
            });

            ViewBag.DoanhThuThang = tongDT - tongHH;
            ViewBag.HoaHongThang = tongHH;

            // Biểu đồ 6 tháng
            var bieu = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var t = now.AddMonths(-i);
                var bd = new DateTime(t.Year, t.Month, 1);
                var kt = bd.AddMonths(1);
                var rows = await _context.DatSans
                    .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                    .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                             && d.ThoiGianTao >= bd && d.ThoiGianTao < kt
                             && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                              || d.TrangThai == "DangSuDung"))
                    .ToListAsync();

                var dt = rows.Sum(d => d.TongTien > 0 ? d.TongTien : d.TienCoc);
                var hh = rows.Sum(d =>
                {
                    var tyLe = LayTyLe(tyLeMap, d.KhungGio?.SanBong?.Quan);
                    return (d.TongTien > 0 ? d.TongTien : d.TienCoc) * tyLe;
                });
                bieu.Add(new { thang = t.ToString("MM/yyyy"), dt = (double)(dt - hh), soLuot = rows.Count });
            }
            ViewBag.Bieu6Thang = bieu;

            // Đếm tổng đơn ChoDuyet mọi ngày (không chỉ hôm nay) — dùng cho badge navbar
            ViewBag.SoDonChoDuyet = await _context.DatSans
                .Include(d => d.KhungGio)
                .CountAsync(d => sanIds.Contains(d.KhungGio.SanBongId)
                              && d.TrangThai == "ChoDuyet");

            return View();
        }

        // ══════════════════════════════════════════════════════════
        // 2. DANH SÁCH SÂN
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> DanhSachSan()
        {
            var list = await SanCuaToi()
                .Include(s => s.KhungGios)
                .OrderByDescending(s => s.Id).ToListAsync();

            ViewBag.TyLeMap = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.VungKhuVuc != null)
                .ToDictionaryAsync(q => q.TenQuan, q => q.VungKhuVuc!.TyLeHoaHong);

            return View(list);
        }

        // ══════════════════════════════════════════════════════════
        // 3. ĐĂNG KÝ SÂN + HỢP ĐỒNG ĐIỆN TỬ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> DangKySan()
        {
            ViewBag.Quans = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.IsActive)
                .OrderBy(q => q.ThuTu).ToListAsync();
            ViewBag.LoaiSans = await _context.DanhMucLoaiSans.Where(l => l.IsActive).ToListAsync();
            ViewBag.LoaiCos = await _context.DanhMucLoaiCos.Where(l => l.IsActive).ToListAsync();
            return View();
        }

        // POST bước 1: lưu thông tin sân tạm, chuyển sang trang HĐ
        [HttpPost]
        public async Task<IActionResult> XemHopDong(
            string tenSan, string diaChi, string quan, string thanhPho,
            string loaiSan, string loaiCo, string? moTa,
            double latitude, double longitude, decimal tyLeCoc)
        {
            // Tra tỷ lệ hoa hồng theo quận
            var danhMucQuan = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .FirstOrDefaultAsync(q => q.TenQuan == quan && q.IsActive);
            var tyLeHH = danhMucQuan?.VungKhuVuc?.TyLeHoaHong ?? 0.10m;
            var tenVung = danhMucQuan?.VungKhuVuc?.TenVung ?? "Chưa phân vùng";

            TempData["San_TenSan"] = tenSan;
            TempData["San_DiaChi"] = diaChi;
            TempData["San_Quan"] = quan;
            TempData["San_ThanhPho"] = thanhPho;
            TempData["San_LoaiSan"] = loaiSan;
            TempData["San_LoaiCo"] = loaiCo;
            TempData["San_MoTa"] = moTa ?? "";
            TempData["San_Lat"] = latitude.ToString();
            TempData["San_Lng"] = longitude.ToString();
            TempData["San_TyLeCoc"] = tyLeCoc.ToString();
            TempData["San_TyLeHH"] = tyLeHH.ToString();
            TempData["San_TenVung"] = tenVung;

            var ownerId = GetOwnerId();
            var owner = await _context.Users.FindAsync(ownerId);
            var ngayKy = DateTime.Now;

            var hopDong = $@"HỢP ĐỒNG HỢP TÁC SỬ DỤNG NỀN TẢNG PITCHHUB
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Ngày lập hợp đồng: {ngayKy:dd/MM/yyyy HH:mm}

BÊN A (PitchHub): Công ty TNHH PitchHub Việt Nam
BÊN B (Chủ sân): {owner?.HoTen} — {owner?.Email} — {owner?.SoDienThoai}

ĐIỀU 1 — ĐỐI TƯỢNG HỢP ĐỒNG
Bên B đăng ký đưa cơ sở ""{tenSan}"" tại {diaChi}, {quan}, {thanhPho}
lên nền tảng PitchHub để tiếp cận khách hàng đặt sân trực tuyến.
Loại sân: {loaiSan} người | Loại cỏ: {loaiCo}
Khu vực phân vùng: {tenVung}

ĐIỀU 2 — PHÍ HOA HỒNG
Căn cứ vào khu vực {quan} thuộc vùng ""{tenVung}"",
tỷ lệ hoa hồng áp dụng là {tyLeHH:P0} tính trên doanh thu thực phát sinh:
  - Kịch bản A (khách hủy sân): Hoa hồng = Tiền cọc × {tyLeHH:P0}
  - Kịch bản B (khách đến đá đủ): Hoa hồng = Tổng tiền × {tyLeHH:P0}
Thanh toán định kỳ hàng tháng, chậm nhất ngày 10 tháng kế tiếp.

ĐIỀU 3 — QUYỀN VÀ NGHĨA VỤ BÊN B
- Cung cấp thông tin sân trung thực, đầy đủ và cập nhật kịp thời.
- Đảm bảo chất lượng dịch vụ phù hợp với mô tả trên hệ thống.
- Không hủy đặt sân của khách hàng quá 3 lần/tháng.
- Thực hiện đúng chính sách hoàn cọc theo quy định PitchHub.

ĐIỀU 4 — QUYỀN VÀ NGHĨA VỤ BÊN A
- Cung cấp nền tảng ổn định, hỗ trợ kỹ thuật 24/7.
- Quảng bá sân đến khách hàng trên toàn hệ thống.
- Thanh toán phần doanh thu sau khi trừ hoa hồng đúng hạn.

ĐIỀU 5 — THỜI HẠN
Hợp đồng có hiệu lực 12 tháng kể từ ngày Admin phê duyệt.
Tự động gia hạn thêm 12 tháng nếu không có thông báo chấm dứt trước 30 ngày.

ĐIỀU 6 — CHẤM DỨT HỢP ĐỒNG
Một trong hai bên có thể chấm dứt bằng văn bản/email thông báo trước 30 ngày.
PitchHub có quyền chấm dứt ngay lập tức nếu Bên B vi phạm Điều 3.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Hợp đồng được lập điện tử, có giá trị pháp lý tương đương hợp đồng giấy.
Bên B xác nhận đã đọc, hiểu và đồng ý toàn bộ các điều khoản trên.";

            TempData["HopDong"] = hopDong;
            return View();
        }

        // POST bước 2: Owner tick đồng ý → lưu sân vào DB
        [HttpPost]
        public async Task<IActionResult> XacNhanDangKy(bool daDocVaDongY)
        {
            if (!daDocVaDongY)
            {
                TempData["Error"] = "Bạn cần tick đồng ý điều khoản hợp đồng!";
                return RedirectToAction("DangKySan");
            }

            var ownerId = GetOwnerId();
            var san = new SanBong
            {
                TenSan = TempData["San_TenSan"]?.ToString() ?? "",
                DiaChi = TempData["San_DiaChi"]?.ToString() ?? "",
                Quan = TempData["San_Quan"]?.ToString() ?? "",
                ThanhPho = TempData["San_ThanhPho"]?.ToString() ?? "",
                LoaiSan = TempData["San_LoaiSan"]?.ToString() ?? "",
                LoaiCo = TempData["San_LoaiCo"]?.ToString() ?? "",
                MoTa = TempData["San_MoTa"]?.ToString() ?? "",
                Latitude = double.TryParse(TempData["San_Lat"]?.ToString(), out var lat) ? lat : 0,
                Longitude = double.TryParse(TempData["San_Lng"]?.ToString(), out var lng) ? lng : 0,
                TyLeCoc = decimal.TryParse(TempData["San_TyLeCoc"]?.ToString(), out var coc) ? coc : 0.30m,
                DaKyHopDong = true,
                NgayKyHopDong = DateTime.Now,
                NoiDungHopDong = TempData["HopDong"]?.ToString(),
                TrangThaiDuyet = "ChoDuyet",
                OwnerId = ownerId
            };

            _context.SanBongs.Add(san);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã gửi đăng ký sân \"{san.TenSan}\"! Admin sẽ xem xét và phê duyệt sớm nhất.";
            return RedirectToAction("DanhSachSan");
        }
        // ══════════════════════════════════════════════════════════
        // 4. SỬA THÔNG TIN SÂN
        // ══════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> SuaSan(int id)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == id);
            if (san == null) return NotFound();
            ViewBag.LoaiSans = await _context.DanhMucLoaiSans.Where(l => l.IsActive).ToListAsync();
            ViewBag.LoaiCos = await _context.DanhMucLoaiCos.Where(l => l.IsActive).ToListAsync();
            return View(san);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuaSan(int id, string moTa, decimal tyLeCoc, bool isHidden,
            int thoiGianGiuCho, int thoiGianHuyTruocGioDa,
            decimal phanTramHoanCocDungHan, decimal phanTramHoanCocTreHan)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == id);
            if (san == null) return NotFound();

            san.MoTa = moTa;
            san.TyLeCoc = tyLeCoc;
            san.IsHidden = isHidden;
            san.ThoiGianGiuCho = thoiGianGiuCho;
            san.ThoiGianHuyTruocGioDa = thoiGianHuyTruocGioDa;
            san.PhanTramHoanCocDungHan = phanTramHoanCocDungHan / 100;   // từ % sang decimal
            san.PhanTramHoanCocTreHan = phanTramHoanCocTreHan / 100;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật cấu hình cho sân \"{san.TenSan}\".";
            return RedirectToAction("DanhSachSan");
        }
        // POST: /Owner/ToggleHideSan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleHideSan(int id)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == id);
            if (san == null) return NotFound();

            san.IsHidden = !san.IsHidden;
            await _context.SaveChangesAsync();

            TempData["Success"] = san.IsHidden
                ? $"Sân \"{san.TenSan}\" đã được ẩn khỏi danh sách tìm kiếm."
                : $"Sân \"{san.TenSan}\" đã được hiển thị trở lại.";

            return RedirectToAction("DanhSachSan");
        }
        // ══════════════════════════════════════════════════════════
        // 5. KHUNG GIỜ & BẢNG GIÁ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> KhungGio(int sanId)
        {
            var san = await SanCuaToi()
                .Include(s => s.KhungGios)
                .FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();
            ViewBag.San = san;
            return View(san.KhungGios.OrderBy(k => k.LoaiNgay).ThenBy(k => k.GioBatDau).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemKhungGio(
            int sanId,
            string gioBatDau,
            string gioKetThuc,
            string loaiNgay,
            decimal? gia = null,
            decimal? giaGioVang = null,
            decimal? giaCuoiTuan = null)
        {
            if (!TimeSpan.TryParse(gioBatDau, out var gioBD) || !TimeSpan.TryParse(gioKetThuc, out var gioKT))
            {
                TempData["Error"] = "Giờ không hợp lệ.";
                return RedirectToAction("KhungGio", new { sanId });
            }

            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();

            var gioBDTime = TimeOnly.FromTimeSpan(gioBD);
            var gioKTTime = TimeOnly.FromTimeSpan(gioKT);

            // Kiểm tra trùng khung giờ (cùng loại ngày, cùng sân)
            var conflict = await _context.KhungGios
                .Where(k => k.SanBongId == sanId && k.LoaiNgay == loaiNgay)
                .Where(k => (gioBDTime >= k.GioBatDau && gioBDTime < k.GioKetThuc) ||
                            (gioKTTime > k.GioBatDau && gioKTTime <= k.GioKetThuc) ||
                            (gioBDTime <= k.GioBatDau && gioKTTime >= k.GioKetThuc))
                .AnyAsync();

            if (conflict)
            {
                TempData["Error"] = "Khung giờ bị trùng với khung đã có!";
                return RedirectToAction("KhungGio", new { sanId });
            }

            var kg = new KhungGio
            {
                SanBongId = sanId,
                GioBatDau = gioBDTime,
                GioKetThuc = gioKTTime,
                LoaiNgay = loaiNgay,
                TrangThai = "Trong"
            };

            switch (loaiNgay)
            {
                case "NgayThuong":
                    kg.Gia = gia ?? 0;
                    kg.GiaGioVang = giaGioVang ?? 0;
                    break;
                case "CuoiTuan":
                    kg.GiaCuoiTuan = giaCuoiTuan ?? 0;
                    kg.GiaGioVang = giaGioVang ?? 0;
                    break;
                default: // "TatCa"
                    kg.Gia = gia ?? 0;
                    kg.GiaGioVang = giaGioVang ?? 0;
                    kg.GiaCuoiTuan = giaCuoiTuan ?? 0;
                    break;
            }

            _context.KhungGios.Add(kg);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm khung giờ!";
            return RedirectToAction("KhungGio", new { sanId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuaKhungGio(int id, decimal? gia, decimal? giaGioVang, decimal? giaCuoiTuan)
        {
            var kg = await _context.KhungGios.FindAsync(id);
            if (kg == null) return NotFound();

            // Kiểm tra quyền (Owner của sân đó)
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == kg.SanBongId);
            if (san == null) return Unauthorized();

            // Cập nhật các giá trị (nếu có)
            if (gia.HasValue) kg.Gia = gia.Value;
            if (giaGioVang.HasValue) kg.GiaGioVang = giaGioVang.Value;
            if (giaCuoiTuan.HasValue) kg.GiaCuoiTuan = giaCuoiTuan.Value;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật giá khung giờ!";
            return RedirectToAction("KhungGio", new { sanId = kg.SanBongId });
        }

        [HttpPost]
        public async Task<IActionResult> XoaKhungGio(int id)
        {
            var kg = await _context.KhungGios
                .Include(k => k.SanBong)
                .FirstOrDefaultAsync(k => k.Id == id && k.SanBong.OwnerId == GetOwnerId());
            if (kg == null) return NotFound();
            if (kg.TrangThai == "DaDat")
            {
                TempData["Error"] = "Không thể xoá khung giờ đã có người đặt!";
                return RedirectToAction("KhungGio", new { sanId = kg.SanBongId });
            }
            var sanId = kg.SanBongId;
            _context.KhungGios.Remove(kg);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xoá khung giờ!";
            return RedirectToAction("KhungGio", new { sanId });
        }
        public async Task<IActionResult> GoiYGiaSan()
        {
            try
            {
                var san = await SanCuaToi().FirstOrDefaultAsync();
                if (san == null)
                {
                    ViewBag.Error = "Chưa có sân để gợi ý.";
                    return View();
                }

                var requestData = new { sanId = san.Id, month = DateTime.Now.Month, year = DateTime.Now.Year };
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("http://localhost:8080/");
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.PostAsJsonAsync("/api/owner/suggest-price", requestData);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;
                    var suggestion = new
                    {
                        suggestedGoldPrice = root.GetProperty("suggestedGoldPrice").GetDecimal(),
                        suggestedRegularPrice = root.GetProperty("suggestedRegularPrice").GetDecimal(),
                        reason = root.GetProperty("reason").GetString() ?? "",
                        confidence = root.GetProperty("confidence").GetDouble()
                    };
                    ViewBag.Suggestion = suggestion;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    ViewBag.Error = $"Java service trả về lỗi: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Lỗi kết nối Java: {ex.Message}";
            }

            return View();
        }
        // ══════════════════════════════════════════════════════════
        // 6. QUẢN LÝ STAFF
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> QuanLyStaff()
        {
            var ownerId = GetOwnerId();
            var staffList = await _context.Users
                .Include(u => u.StaffSanPhanCongs).ThenInclude(p => p.SanBong)
                .Where(u => u.VaiTro == "Staff" && u.OwnerIdCuaStaff == ownerId)
                .ToListAsync();
            ViewBag.DanhSachSan = await SanCuaToi()
                .Where(s => s.TrangThaiDuyet == "DaDuyet").ToListAsync();
            return View(staffList);
        }

        [HttpPost]
        public async Task<IActionResult> TaoStaff(string hoTen, string email,
            string matKhau, string soDienThoai, int sanBongId)
        {
            var ownerId = GetOwnerId();
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanBongId);
            if (san == null)
            { TempData["Error"] = "Sân không hợp lệ!"; return RedirectToAction("QuanLyStaff"); }

            if (await _context.Users.AnyAsync(u => u.Email == email))
            { TempData["Error"] = $"Email {email} đã tồn tại!"; return RedirectToAction("QuanLyStaff"); }

            var staff = new User
            {
                HoTen = hoTen,
                Email = email,
                MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhau),
                SoDienThoai = soDienThoai,
                VaiTro = "Staff",
                IsActive = true,
                OwnerIdCuaStaff = ownerId,
                NgayTao = DateTime.Now
            };
            _context.Users.Add(staff);
            await _context.SaveChangesAsync();

            _context.StaffSanPhanCongs.Add(new StaffSanPhanCong
            {
                StaffId = staff.Id,
                SanBongId = sanBongId
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã tạo tài khoản Staff \"{hoTen}\" và gán vào \"{san.TenSan}\"!";
            return RedirectToAction("QuanLyStaff");
        }

        [HttpPost]
        public async Task<IActionResult> GanSanChoStaff(int staffId, int sanBongId)
        {
            var ownerId = GetOwnerId();
            var staff = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == staffId && u.OwnerIdCuaStaff == ownerId);
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanBongId);
            if (staff == null || san == null) return NotFound();

            var daGan = await _context.StaffSanPhanCongs
                .AnyAsync(p => p.StaffId == staffId && p.SanBongId == sanBongId);
            if (!daGan)
            {
                _context.StaffSanPhanCongs.Add(new StaffSanPhanCong
                {
                    StaffId = staffId,
                    SanBongId = sanBongId
                });
                await _context.SaveChangesAsync();
            }
            TempData["Success"] = $"Đã gán {staff.HoTen} vào \"{san.TenSan}\"!";
            return RedirectToAction("QuanLyStaff");
        }
        // POST: /Owner/XoaGanSanChoStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaGanSanChoStaff(int staffId, int sanBongId)
        {
            var ownerId = GetOwnerId();
            var staff = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == staffId && u.OwnerIdCuaStaff == ownerId);
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanBongId);
            if (staff == null || san == null) return NotFound();

            var gan = await _context.StaffSanPhanCongs
                .FirstOrDefaultAsync(p => p.StaffId == staffId && p.SanBongId == sanBongId);
            if (gan != null)
            {
                _context.StaffSanPhanCongs.Remove(gan);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã gỡ nhân viên {staff.HoTen} khỏi sân {san.TenSan}.";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy phân công này.";
            }
            return RedirectToAction("QuanLyStaff");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KhoaStaff(int staffId, bool isActive)
        {
            var ownerId = GetOwnerId();
            var staff = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == staffId && u.OwnerIdCuaStaff == ownerId);
            if (staff == null) return NotFound();

            staff.IsActive = isActive;   // isActive từ form: true = mở, false = khóa
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{(isActive ? "Mở khoá" : "Khoá")} tài khoản {staff.HoTen}!";
            return RedirectToAction("QuanLyStaff");
        }
        // ══════════════════════════════════════════════════════════
        // 7. KHO DỊCH VỤ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> KhoDichVu(int sanId)
        {
            var san = await SanCuaToi()
                .Include(s => s.DichVus).ThenInclude(d => d.DanhMucDichVu)
                .FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();
            ViewBag.San = san;
            ViewBag.DanhMucChuaBat = await _context.DanhMucDichVus
                .Where(dm => dm.IsActive &&
                    !san.DichVus.Select(d => d.DanhMucDichVuId).Contains(dm.Id))
                .ToListAsync();
            return View(san.DichVus.OrderBy(d => d.DanhMucDichVu.TenDichVu).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> BatDichVu(int sanId, int danhMucId,
            decimal gia, int tonKho)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanId);
            var dm = await _context.DanhMucDichVus.FindAsync(danhMucId);
            if (san == null || dm == null) return NotFound();

            _context.DichVus.Add(new DichVu
            {
                SanBongId = sanId,
                DanhMucDichVuId = danhMucId,
                TenDichVu = dm.TenDichVu,
                Gia = gia,
                TonKho = tonKho,
                IsActive = true
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã bật dịch vụ \"{dm.TenDichVu}\" cho sân!";
            return RedirectToAction("KhoDichVu", new { sanId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatDichVu(int dichVuId, decimal gia, int tonKho, bool isActive)
        {
            var dv = await _context.DichVus
                .Include(d => d.SanBong)
                .FirstOrDefaultAsync(d => d.Id == dichVuId && d.SanBong.OwnerId == GetOwnerId());
            if (dv == null)
            {
                TempData["Error"] = "Không tìm thấy dịch vụ hoặc bạn không có quyền.";
                // Quay lại trang trước đó (có thể lấy sanId từ session hoặc từ dv cũ? Không có dv thì không biết sanId)
                // Tạm thời redirect về DanhSachSan
                return RedirectToAction("DanhSachSan");
            }

            dv.Gia = gia;
            dv.TonKho = tonKho;
            dv.IsActive = isActive;

            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new Web_Stadium.EFCore.AuditLog
            {
                UserId = GetOwnerId(),
                VaiTro = "Owner",
                HanhDong = "CapNhatDichVu",
                DoiTuong = "DichVu",
                DoiTuongId = dichVuId,
                MoTa = $"Cập nhật {dv.TenDichVu}: Giá={gia:N0}, TonKho={tonKho}, Active={isActive}"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật dịch vụ!";
            return RedirectToAction("KhoDichVu", new { sanId = dv.SanBongId });
        }
        // ===================== QUẢN LÝ ĐÁNH GIÁ & PHẢN HỒI =====================

        // GET: /Owner/DanhSachDanhGia
        public async Task<IActionResult> DanhSachDanhGia(int? sanId = null, int? soSao = null)
        {
            var ownerId = GetOwnerId();
            var query = _context.DanhGias
                .Include(d => d.SanBong)
                .Include(d => d.User)
                .Include(d => d.DatSan)
                .Where(d => d.SanBong.OwnerId == ownerId);

            if (sanId.HasValue && sanId.Value > 0)
            {
                query = query.Where(d => d.SanBongId == sanId.Value);
            }

            if (soSao.HasValue && soSao.Value >= 1 && soSao.Value <= 5)
            {
                query = query.Where(d => d.SoSao == soSao.Value);
            }

            var danhSach = await query
                .OrderByDescending(d => d.NgayDanhGia)
                .ToListAsync();

            // Lấy danh sách sân của owner để hiển thị dropdown lọc
            var dsSan = await SanCuaToi()
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .Select(s => new { s.Id, s.TenSan })
                .ToListAsync();
            ViewBag.DanhSachSan = dsSan;
            ViewBag.SelectedSanId = sanId;
            ViewBag.SelectedSoSao = soSao;

            return View(danhSach);
        }

        // POST: /Owner/PhanHoiDanhGia
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PhanHoiDanhGia(int id, string phanHoi)
        {
            var danhGia = await _context.DanhGias
                .Include(d => d.SanBong)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (danhGia == null) return NotFound();

            var ownerId = GetOwnerId();
            if (danhGia.SanBong.OwnerId != ownerId)
                return Unauthorized();

            // Lưu phản hồi (cần thêm cột PhanHoiOwner trong bảng DanhGias nếu chưa có)
            // Nếu chưa có cột, thêm migration hoặc dùng NotMapped + lưu riêng bảng PhanHoiDanhGia
            // Ở đây giả sử bạn đã có cột PhanHoiOwner (nvarchar(max)) trong bảng DanhGias
            danhGia.PhanHoiOwner = phanHoi;
            danhGia.NgayPhanHoi = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi phản hồi đến khách hàng.";
            return RedirectToAction("DanhSachDanhGia", new { sanId = ViewBag.SelectedSanId, soSao = ViewBag.SelectedSoSao });
        }
        [HttpPost]
        public async Task<IActionResult> XemHopDongPreview(
    string tenSan, string diaChi, string quan, string thanhPho,
    string loaiSan, string loaiCo, string? moTa,
    double latitude, double longitude, decimal tyLeCoc)
        {
            // Tra tỷ lệ hoa hồng theo quận
            var danhMucQuan = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .FirstOrDefaultAsync(q => q.TenQuan == quan && q.IsActive);
            var tyLeHH = danhMucQuan?.VungKhuVuc?.TyLeHoaHong ?? 0.10m;
            var tenVung = danhMucQuan?.VungKhuVuc?.TenVung ?? "Chưa phân vùng";

            // Tạo đối tượng SanBong tạm thời từ dữ liệu nhập
            var san = new SanBong
            {
                Id = 0,  // đánh dấu là chưa có trong DB
                TenSan = tenSan,
                DiaChi = diaChi,
                Quan = quan,
                ThanhPho = thanhPho,
                LoaiSan = loaiSan,
                LoaiCo = loaiCo,
                MoTa = moTa ?? "",
                Latitude = latitude,
                Longitude = longitude,
                TyLeCoc = tyLeCoc,
                OwnerId = GetOwnerId()
            };

            // Lưu tạm thông tin vào TempData để khi submit form xác nhận sẽ dùng lại
            TempData["San_TenSan"] = tenSan;
            TempData["San_DiaChi"] = diaChi;
            TempData["San_Quan"] = quan;
            TempData["San_ThanhPho"] = thanhPho;
            TempData["San_LoaiSan"] = loaiSan;
            TempData["San_LoaiCo"] = loaiCo;
            TempData["San_MoTa"] = moTa ?? "";
            TempData["San_Lat"] = latitude.ToString();
            TempData["San_Lng"] = longitude.ToString();
            TempData["San_TyLeCoc"] = tyLeCoc.ToString();
            TempData["San_TyLeHH"] = tyLeHH.ToString();
            TempData["San_TenVung"] = tenVung;

            // Lấy thông tin chủ sân (hiện tại)
            var owner = await _context.Users.FindAsync(GetOwnerId());
            ViewBag.OwnerName = owner?.HoTen;
            ViewBag.OwnerEmail = owner?.Email;
            ViewBag.OwnerPhone = owner?.SoDienThoai;

            ViewBag.TyLeHH = tyLeHH;
            ViewBag.TenVung = tenVung;
            ViewBag.IsPreview = true;   // đánh dấu đang ở chế độ xem trước

            // Lưu plain text hợp đồng để khi user xác nhận sẽ lưu vào DB (NoiDungHopDong).
            var ngayKy = DateTime.Now;
            TempData["HopDong"] = $@"HỢP ĐỒNG HỢP TÁC KINH DOANH PITCHHUB
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Ngày lập: {ngayKy:dd/MM/yyyy HH:mm}

BÊN A: Công ty TNHH PitchHub Việt Nam
BÊN B: {owner?.HoTen} — {owner?.Email} — {owner?.SoDienThoai}

ĐIỀU 1 — ĐỐI TƯỢNG HỢP ĐỒNG
Bên B đăng ký đưa cơ sở ""{tenSan}"" tại {diaChi}, {quan}, {thanhPho}
lên nền tảng PitchHub. Loại sân: {loaiSan} người | Loại cỏ: {loaiCo}
Khu vực phân vùng: {tenVung}

ĐIỀU 2 — PHÍ HOA HỒNG
Tỷ lệ hoa hồng áp dụng: {tyLeHH:P0} doanh thu thực phát sinh.
- Khách hủy sân: Hoa hồng = Tiền cọc × {tyLeHH:P0}
- Khách đến đá đủ: Hoa hồng = Tổng tiền × {tyLeHH:P0}
Thanh toán định kỳ hàng tháng, chậm nhất ngày 10 tháng kế tiếp.

ĐIỀU 3 — THỜI HẠN
Hợp đồng có hiệu lực 12 tháng kể từ ngày Admin phê duyệt.

ĐIỀU 4 — XÁC NHẬN
Bên B xác nhận đã đọc, hiểu và đồng ý toàn bộ điều khoản.";

            return View("XemHopDong", san);
        }
        // GET: /Owner/XemHopDong/{id}
        public async Task<IActionResult> XemHopDong(int id)
        {
            var san = await SanCuaToi()
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (san == null) return NotFound();

            ViewBag.IsPreview = false;
            return View(san);
        }
        // ===================== QUẢN LÝ ĐƠN ĐẶT SÂN =====================

        // GET: /Owner/DanhSachDonDat
        public async Task<IActionResult> DanhSachDonDat(string status = "ChoDuyet")
        {
            var ownerId = GetOwnerId();
            var query = _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.KhungGio.SanBong.OwnerId == ownerId);

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(d => d.TrangThai == status);
            }

            var dsDon = await query
                .OrderByDescending(d => d.NgayThiDau)
                .ThenBy(d => d.KhungGio.GioBatDau)
                .ToListAsync();

            // ── Load lý do từ chối (cho các đơn DaHuy) từ AuditLogs ──
            // AuditLog ghi khi từ chối có dạng MoTa = "Từ chối đơn {Ma} — Lý do: {lyDo}"
            var idsDaHuy = dsDon.Where(d => d.TrangThai == "DaHuy").Select(d => d.Id).ToList();
            var lyDoMap = new Dictionary<int, (string LyDo, DateTime ThoiGian, string NguoiXuLy)>();
            if (idsDaHuy.Any())
            {
                var logs = await (from a in _context.AuditLogs
                                  join u in _context.Users on a.UserId equals u.Id into gu
                                  from u in gu.DefaultIfEmpty()
                                  where a.DoiTuong == "DatSan"
                                        && a.HanhDong == "TuChoiDon"
                                        && idsDaHuy.Contains(a.DoiTuongId)
                                  orderby a.ThoiGian descending
                                  select new { a.DoiTuongId, a.MoTa, a.ThoiGian, NguoiXuLy = u != null ? u.HoTen : "Owner" })
                                 .ToListAsync();

                foreach (var log in logs)
                {
                    if (lyDoMap.ContainsKey(log.DoiTuongId)) continue; // chỉ giữ bản ghi mới nhất
                    var lyDo = log.MoTa ?? "";
                    var marker = "Lý do:";
                    var idx = lyDo.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0) lyDo = lyDo.Substring(idx + marker.Length).Trim();
                    if (string.IsNullOrWhiteSpace(lyDo)) lyDo = "(Không có lý do)";
                    lyDoMap[log.DoiTuongId] = (lyDo, log.ThoiGian, log.NguoiXuLy ?? "Owner");
                }
            }
            ViewBag.LyDoTuChoi = lyDoMap;

            ViewBag.CurrentStatus = status;
            return View(dsDon);
        }

        // ================================================================
        // FILE: OwnerController_QuanLyAnh.cs
        // Thay toàn bộ #region Quản lý ảnh sân trong OwnerController.cs
        // bằng đoạn dưới đây. Xóa hết code cũ trong region đó đi.
        // ================================================================

        #region Quản lý ảnh sân

        // GET /Owner/QuanLyAnh
        public async Task<IActionResult> QuanLyAnh()
        {
            var dsSan = await SanCuaToi()
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .OrderBy(s => s.TenSan)
                .ToListAsync();

            if (!dsSan.Any())
                return Content("Chưa có sân nào được duyệt.");

            var sanIds = dsSan.Select(s => s.Id).ToList();
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
            return View(dsSan);
        }

        // GET /Owner/QuanLyAnhChiTiet?sanId=...
        public async Task<IActionResult> QuanLyAnhChiTiet(int sanId)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();

            var dsAnh = await _context.AnhSanBongs
                .Where(a => a.SanBongId == sanId && a.IsActive)
                .OrderBy(a => a.ThuTu)
                .ToListAsync();

            ViewBag.San = san;
            return View(dsAnh);
        }

       // ================================================================
// Thêm action này vào OwnerController.cs
// Đặt bên trong #region Quản lý ảnh sân, cạnh các action khác
// ================================================================

// POST /Owner/UploadAnhUrl
// Nhận danh sách URL, lưu trực tiếp vào DB (không download file)
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UploadAnhUrl(int sanId, List<string> urls)
{
    int ownerId;
    try { ownerId = GetOwnerId(); }
    catch
    {
        TempData["Error"] = "Phiên đăng nhập hết hạn.";
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    var san = await _context.SanBongs
        .FirstOrDefaultAsync(s => s.Id == sanId && s.OwnerId == ownerId);
    if (san == null)
    {
        TempData["Error"] = "Sân không hợp lệ.";
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    if (urls == null || !urls.Any())
    {
        TempData["Error"] = "Vui lòng nhập ít nhất 1 URL.";
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    // Lọc các URL hợp lệ (không rỗng, bắt đầu bằng http)
    var validUrls = urls
        .Where(u => !string.IsNullOrWhiteSpace(u))
        .Where(u => u.StartsWith("http://") || u.StartsWith("https://"))
        .Select(u => u.Trim())
        .Distinct()
        .ToList();

    if (!validUrls.Any())
    {
        TempData["Error"] = "Không có URL hợp lệ nào. URL phải bắt đầu bằng http:// hoặc https://";
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    int count = 0;
    foreach (var url in validUrls)
    {
        // Kiểm tra URL chưa tồn tại trong DB
        var exists = await _context.AnhSanBongs
            .AnyAsync(a => a.SanBongId == sanId && a.DuongDan == url && a.IsActive);
        if (exists) continue;

        _context.AnhSanBongs.Add(new AnhSanBong
        {
            SanBongId = sanId,
            DuongDan  = url,
            LoaiAnh   = "Url",
            ThuTu     = 0,
            NgayThem  = DateTime.Now,
            IsActive  = true
        });
        count++;
    }

    if (count == 0)
    {
        TempData["Error"] = "Tất cả URL đã tồn tại hoặc không hợp lệ.";
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    await _context.SaveChangesAsync();
    await ReorderImages(sanId);

    TempData["Success"] = $"Đã thêm {count} ảnh thành công!";
    return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
}

// POST /Owner/UploadAnhFile
// Nhận file ảnh upload trực tiếp từ máy, lưu vào wwwroot/uploads/san_{id}/
[HttpPost]
[ValidateAntiForgeryToken]
[RequestSizeLimit(20 * 1024 * 1024)] // tối đa 20MB cho cả request
public async Task<IActionResult> UploadAnhFile(int sanId, List<IFormFile> files)
{
    int ownerId;
    try { ownerId = GetOwnerId(); }
    catch
    {
        TempData["Error"] = "Phiên đăng nhập hết hạn.";
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    var san = await _context.SanBongs
        .FirstOrDefaultAsync(s => s.Id == sanId && s.OwnerId == ownerId);
    if (san == null)
    {
        TempData["Error"] = "Sân không hợp lệ.";
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    if (files == null || !files.Any(f => f != null && f.Length > 0))
    {
        TempData["Error"] = "Vui lòng chọn ít nhất 1 ảnh.";
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    const long maxFileSize = 5 * 1024 * 1024; // 5MB / ảnh

    var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", $"san_{sanId}");
    Directory.CreateDirectory(folder);

    int saved = 0;
    var skipped = new List<string>();

    foreach (var file in files)
    {
        if (file == null || file.Length == 0) continue;

        if (file.Length > maxFileSize)
        {
            skipped.Add($"{file.FileName} (quá 5MB)");
            continue;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExt.Contains(ext))
        {
            skipped.Add($"{file.FileName} (định dạng không hỗ trợ)");
            continue;
        }

        var fileName = $"san_{sanId}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(folder, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var duongDan = $"/uploads/san_{sanId}/{fileName}";

        _context.AnhSanBongs.Add(new AnhSanBong
        {
            SanBongId = sanId,
            DuongDan  = duongDan,
            LoaiAnh   = "File",
            ThuTu     = 0,
            NgayThem  = DateTime.Now,
            IsActive  = true
        });
        saved++;
    }

    if (saved == 0)
    {
        TempData["Error"] = "Không upload được ảnh nào. " + string.Join("; ", skipped);
        return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
    }

    await _context.SaveChangesAsync();
    await ReorderImages(sanId);

    if (skipped.Any())
        TempData["Success"] = $"Đã upload {saved} ảnh. Bỏ qua: {string.Join("; ", skipped)}";
    else
        TempData["Success"] = $"Đã upload {saved} ảnh thành công!";

    return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
}

        // POST /Owner/XoaAnhForm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaAnhForm(int id, int sanId)
        {
            int ownerId;
            try { ownerId = GetOwnerId(); }
            catch
            {
                TempData["Error"] = "Phiên đăng nhập hết hạn.";
                return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
            }

            var anh = await _context.AnhSanBongs.FindAsync(id);
            if (anh == null)
            {
                TempData["Error"] = "Không tìm thấy ảnh.";
                return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
            }

            var san = await _context.SanBongs
                .FirstOrDefaultAsync(s => s.Id == anh.SanBongId && s.OwnerId == ownerId);
            if (san == null)
            {
                TempData["Error"] = "Bạn không có quyền.";
                return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
            }

            var filePath = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot",
                anh.DuongDan.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _context.AnhSanBongs.Remove(anh);
            await _context.SaveChangesAsync();
            await ReorderImages(anh.SanBongId);

            TempData["Success"] = "Đã xóa ảnh.";
            return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
        }

        // POST /Owner/SetImagePrimaryForm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetImagePrimaryForm(int imageId, int sanId)
        {
            int ownerId;
            try { ownerId = GetOwnerId(); }
            catch
            {
                TempData["Error"] = "Phiên đăng nhập hết hạn.";
                return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
            }

            var target = await _context.AnhSanBongs.FindAsync(imageId);
            if (target == null)
            {
                TempData["Error"] = "Không tìm thấy ảnh.";
                return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
            }

            var san = await _context.SanBongs
                .FirstOrDefaultAsync(s => s.Id == target.SanBongId && s.OwnerId == ownerId);
            if (san == null)
            {
                TempData["Error"] = "Bạn không có quyền.";
                return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
            }

            if (target.ThuTu == 1)
            {
                TempData["Success"] = "Ảnh này đã là ảnh đại diện.";
                return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
            }

            var images = await _context.AnhSanBongs
                .Where(a => a.SanBongId == target.SanBongId && a.IsActive)
                .OrderBy(a => a.ThuTu)
                .ToListAsync();

            int oldOrder = target.ThuTu;
            foreach (var img in images)
            {
                if (img.Id == imageId) img.ThuTu = 1;
                else if (img.ThuTu < oldOrder) img.ThuTu++;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã đặt làm ảnh đại diện!";
            return RedirectToAction("QuanLyAnhChiTiet", new { sanId });
        }

        // HELPER
        private async Task ReorderImages(int sanId)
        {
            var list = await _context.AnhSanBongs
                .Where(a => a.SanBongId == sanId && a.IsActive)
                .OrderBy(a => a.ThuTu)
                .ToListAsync();
            for (int i = 0; i < list.Count; i++)
                list[i].ThuTu = i + 1;
            await _context.SaveChangesAsync();
        }

        #endregion

        // ================================================================
        // REQUEST MODELS — đặt cuối class OwnerController (trước dấu } cuối)
        // Xóa các class cũ XoaAnhRequest, SetPrimaryRequest, UploadBase64Request
        // rồi thay bằng đoạn dưới
        // ================================================================
        public class XoaAnhRequest { public int Id { get; set; } }
        public class SetPrimaryRequest { public int ImageId { get; set; } }

        // ══════════════════════════════════════════════════════════
        // 8. BÁO CÁO CHI NHÁNH
        // ══════════════════════════════════════════════════════════
        // Helper dùng chung cho action BaoCao (render view) và XuatBaoCaoPDF (gửi sang Java)
        private async Task<(List<dynamic> Data, string TieuDe, double TongThuThuan, double TongDT, double TongHoaHong, int TongLuot, List<int> SanIds)>
            BuildBaoCaoDataAsync(string loai, int nam, int thang, int? sanId, List<SanBong> dsSan)
        {
            var sanIds = sanId.HasValue && sanId.Value > 0
                ? new List<int> { sanId.Value }
                : dsSan.Select(s => s.Id).ToList();

            var data = new List<dynamic>();
            if (!sanIds.Any())
                return (data, "Không có sân nào được chọn", 0, 0, 0, 0, sanIds);

            IQueryable<DatSan> baseQ = _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Include(d => d.StaffCheckIn)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                          || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy"));

            var tyLeMap = await LayTyLeMapAsync();
            var now = DateTime.Now;
            string tieuDe;

            switch (loai)
            {
                case "ngay":
                    tieuDe = $"Theo ngày — Tháng {thang}/{nam}";
                    for (int ng = 1; ng <= DateTime.DaysInMonth(nam, thang); ng++)
                    {
                        var bd = new DateTime(nam, thang, ng);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < bd.AddDays(1)).ToListAsync();
                        data.Add(BuildOwnerPoint($"{ng}/{thang}", rows, tyLeMap));
                    }
                    break;
                case "tuan":
                    tieuDe = $"Theo tuần — Tháng {thang}/{nam}";
                    var batDau = new DateTime(nam, thang, 1);
                    var ketThuc = batDau.AddMonths(1);
                    int t = 1; var cur = batDau;
                    while (cur < ketThuc)
                    {
                        var kt = cur.AddDays(7) < ketThuc ? cur.AddDays(7) : ketThuc;
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= cur && d.ThoiGianTao < kt).ToListAsync();
                        data.Add(BuildOwnerPoint($"T{t} ({cur:dd/MM}–{kt.AddDays(-1):dd/MM})", rows, tyLeMap));
                        cur = kt; t++;
                    }
                    break;
                case "quy":
                    tieuDe = $"Theo quý — Năm {nam}";
                    for (int q = 1; q <= 4; q++)
                    {
                        var bd = new DateTime(nam, (q - 1) * 3 + 1, 1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < bd.AddMonths(3)).ToListAsync();
                        data.Add(BuildOwnerPoint($"Q{q}/{nam}", rows, tyLeMap));
                    }
                    break;
                case "nam":
                    tieuDe = "Theo năm — 5 năm gần nhất";
                    for (int y = now.Year - 4; y <= now.Year; y++)
                    {
                        var bd = new DateTime(y, 1, 1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < new DateTime(y + 1, 1, 1)).ToListAsync();
                        data.Add(BuildOwnerPoint($"{y}", rows, tyLeMap));
                    }
                    break;
                default: // thang
                    tieuDe = $"Theo tháng — Năm {nam}";
                    for (int m = 1; m <= 12; m++)
                    {
                        var bd = new DateTime(nam, m, 1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < bd.AddMonths(1)).ToListAsync();
                        data.Add(BuildOwnerPoint($"T{m}", rows, tyLeMap));
                    }
                    break;
            }

            double tongThuThuan = data.Sum(d => (double)d.thuThuan);
            double tongDT = data.Sum(d => (double)d.tongDT);
            double tongHoaHong = data.Sum(d => (double)d.hoaHong);
            int tongLuot = data.Sum(d => (int)d.soLuot);

            return (data, tieuDe, tongThuThuan, tongDT, tongHoaHong, tongLuot, sanIds);
        }

        public async Task<IActionResult> BaoCao(
    string loai = "thang", int? nam = null, int? thang = null, int? sanId = null)
        {
            var now = DateTime.Now;
            nam ??= now.Year;
            thang ??= now.Month;
            ViewBag.Loai = loai; ViewBag.Nam = nam; ViewBag.Thang = thang; ViewBag.SanId = sanId;

            var dsSan = await SanCuaToi().Where(s => s.TrangThaiDuyet == "DaDuyet").ToListAsync();
            ViewBag.DanhSachSan = dsSan;

            var (data, tieuDe, tongThuThuan, tongDT, tongHoaHong, tongLuot, sanIds) =
                await BuildBaoCaoDataAsync(loai, nam.Value, thang.Value, sanId, dsSan);

            if (!sanIds.Any())
            {
                ViewBag.Data = new List<object>();
                ViewBag.TongThuThuan = 0;
                ViewBag.TongLuot = 0;
                ViewBag.TyLeLapDay = new List<object>();
                ViewBag.HieuSuatStaff = new List<object>();
                ViewBag.TieuDe = tieuDe;
                return View();
            }

            ViewBag.Data = data;
            ViewBag.TieuDe = tieuDe;
            ViewBag.TongThuThuan = tongThuThuan;
            ViewBag.TongLuot = tongLuot;

            // Tỷ lệ lấp đầy: nếu có sanId thì chỉ tính cho sân đó, còn không thì tính cho tất cả
            var queryLapDay = SanCuaToi()
                .Include(s => s.KhungGios)
                .Where(s => s.TrangThaiDuyet == "DaDuyet");
            if (sanId.HasValue && sanId.Value > 0)
                queryLapDay = queryLapDay.Where(s => s.Id == sanId.Value);

            ViewBag.TyLeLapDay = await queryLapDay
                .Select(s => new
                {
                    Ten = s.TenSan,
                    Tong = s.KhungGios.Count,
                    DaDat = s.KhungGios.Count(k => k.TrangThai == "DaDat")
                }).ToListAsync();

            // Hiệu suất Staff (cũng lọc theo sanId nếu có)
            var allRows = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.StaffCheckIn)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                          || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy"))
                .ToListAsync();
            ViewBag.HieuSuatStaff = allRows
                .GroupBy(d => d.StaffCheckIn?.HoTen ?? "Chưa check-in")
                .Select(g => new
                {
                    TenStaff = g.Key,
                    SoDon = g.Count(),
                    DoanhThu = g.Sum(d => (double)(d.TongTien > 0 ? d.TongTien : d.TienCoc))
                })
                .OrderByDescending(x => x.SoDon).ToList();

            return View();
        }
        [HttpGet]
        public async Task<IActionResult> XuatBaoCaoPDF(string loai = "thang", int? nam = null, int? thang = null, int? sanId = null)
        {
            string traceFile = Path.Combine(Directory.GetCurrentDirectory(), "app_lifecycle.log");
            void T(string m) { try { System.IO.File.AppendAllText(traceFile, $"[{DateTime.Now:HH:mm:ss.fff}] PDF: {m}\n"); } catch { } }

            T($"START loai={loai} nam={nam} thang={thang} sanId={sanId}");
            var now = DateTime.Now;
            nam ??= now.Year;
            thang ??= now.Month;

            var dsSan = await SanCuaToi().Where(s => s.TrangThaiDuyet == "DaDuyet").ToListAsync();
            var (data, tieuDe, tongThuThuan, tongDT, tongHoaHong, tongLuot, sanIds) =
                await BuildBaoCaoDataAsync(loai, nam.Value, thang.Value, sanId, dsSan);
            T($"Data built: {data.Count} rows, tongDT={tongDT}, tongLuot={tongLuot}");

            string tenSan = sanId.HasValue && sanId.Value > 0
                ? (await _context.SanBongs.FindAsync(sanId.Value))?.TenSan ?? "Tất cả sân"
                : "Tất cả sân";

            string period = loai switch
            {
                "nam"  => $"5 năm gần nhất (đến {now.Year})",
                "quy"  => $"Năm {nam}",
                "thang" => $"Năm {nam}",
                "tuan" => $"Tháng {thang}/{nam}",
                "ngay" => $"Tháng {thang}/{nam}",
                _ => $"{thang}/{nam}"
            };

            var reportData = new
            {
                title = "Báo cáo doanh thu PitchHub",
                stadiumName = tenSan,
                period = $"{tieuDe} — {period}",
                summary = new
                {
                    totalRevenue = tongDT,
                    totalBookings = tongLuot,
                    avgPerBooking = tongLuot > 0 ? tongDT / tongLuot : 0,
                    commission = tongHoaHong,
                    netRevenue = tongThuThuan
                },
                details = data.Select(d => new
                {
                    label = (string)d.nhan,
                    totalRevenue = (double)d.tongDT,
                    bookings = (int)d.soLuot,
                    netRevenue = (double)d.thuThuan
                }).ToList()
            };

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.BaseAddress = new Uri("http://localhost:8080/");
                T("Calling Java service...");
                var response = await client.PostAsJsonAsync("api/report/pdf", reportData);
                T($"Java responded: {(int)response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                    T($"Read {pdfBytes.Length} bytes from Java");
                    string fileName = $"BaoCao_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    T($"Returning File to client: {fileName}");
                    return File(pdfBytes, "application/pdf", fileName);
                }
                var body = await response.Content.ReadAsStringAsync();
                T($"Java error body: {body}");
                TempData["Error"] = $"Lỗi khi sinh PDF: {(int)response.StatusCode} {response.StatusCode}. {body}";
            }
            catch (Exception ex)
            {
                T($"EXCEPTION: {ex.GetType().Name} {ex.Message}");
                TempData["Error"] = "Không kết nối được tới service Java (http://localhost:8080). " + ex.Message;
            }

            T("Redirect to BaoCao");
            return RedirectToAction("BaoCao", new { loai, nam, thang, sanId });
        }
        private object BuildOwnerPoint(string nhan, List<DatSan> rows,
            Dictionary<string, decimal> tyLeMap)
        {
            var tongDT = rows.Sum(d => d.TongTien > 0 ? d.TongTien : d.TienCoc);
            var hh = rows.Sum(d =>
            {
                var tyLe = LayTyLe(tyLeMap, d.KhungGio?.SanBong?.Quan);
                return (d.TongTien > 0 ? d.TongTien : d.TienCoc) * tyLe;
            });
            return new
            {
                nhan = nhan,
                tongDT = (double)tongDT,
                hoaHong = (double)hh,
                thuThuan = (double)(tongDT - hh),
                soLuot = rows.Count
            };
        }
        // ══════════════════════════════════════════════════════════
        // 9. DUYỆT ĐƠN ĐẶT SÂN — điểm nối quan trọng nhất với User
        // ══════════════════════════════════════════════════════════

        // GET /Owner/DuyetDon — danh sách đơn chờ duyệt
        public async Task<IActionResult> DuyetDon()
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            // Đơn chờ duyệt
            ViewBag.DonChoDuyet = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.TrangThai == "ChoDuyet")
                .OrderBy(d => d.ThoiGianTao)
                .ToListAsync();

            // Đơn đã xử lý gần đây (7 ngày)
            ViewBag.DonDaXuLy = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "DaHuy")
                         && d.ThoiGianTao >= DateTime.Now.AddDays(-7))
                .OrderByDescending(d => d.ThoiGianTao)
                .Take(20)
                .ToListAsync();

            return View();
        }

        // POST /Owner/XacNhanDon — Duyệt đơn → gửi email + QR cho khách
        [HttpPost]
        public async Task<IActionResult> XacNhanDon(int datSanId)
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();
            var don = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId)
                                       && d.TrangThai == "ChoDuyet");

            if (don == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hoặc đơn không ở trạng thái Chờ duyệt!";
                return RedirectToAction("DanhSachDonDat");
            }

            don.TrangThai = "DaXacNhan";
            await _context.SaveChangesAsync();

            // ✅ Gửi email xác nhận + QR Code cho khách ngay lập tức
            if (don.User != null && don.KhungGio?.SanBong != null)
            {
                var tenSan = don.KhungGio.SanBong.TenSan;
                var diaChi = don.KhungGio.SanBong.DiaChi + ", " + don.KhungGio.SanBong.Quan;
                var khungGio = $"{don.KhungGio.GioBatDau:HH:mm} – {don.KhungGio.GioKetThuc:HH:mm}";

                await _emailService.GuiEmailXacNhanDatSan(
                    don.User.Email,
                    don.User.HoTen,
                    tenSan, diaChi, khungGio,
                    don.NgayThiDau.ToString("dd/MM/yyyy"),
                    don.MaXacNhan,
                    don.TienCoc);
            }

            // Ghi AuditLog
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = GetOwnerId(),
                VaiTro = "Owner",
                HanhDong = "DuyetDon",
                DoiTuong = "DatSan",
                DoiTuongId = datSanId,
                MoTa = $"Duyệt đơn {don.MaXacNhan} — {don.User?.HoTen}"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Đã duyệt đơn {don.MaXacNhan}! Email xác nhận + QR đã gửi đến khách.";
            return RedirectToAction("DanhSachDonDat");
        }

        // POST /Owner/TuChoiDon — Từ chối đơn → hoàn 100% cọc
        [HttpPost]
        public async Task<IActionResult> TuChoiDon(int datSanId, string lyDo)
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();
            var don = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId)
                                       && d.TrangThai == "ChoDuyet");

            if (don == null)
            {
                TempData["Error"] = "Không tìm thấy đơn!";
                return RedirectToAction("DanhSachDonDat");
            }

            var (success, message) = await _hoanCocService.ThucHienHoanCocAsync(
                don,
                nguonHuy: "OwnerTuChoi",
                vaiTroNguoiKhoiTao: "Owner",
                nguoiKhoiTaoId: GetOwnerId(),
                ghiChu: $"Owner từ chối: {lyDo}"
            );

            if (!success)
            {
                TempData["Error"] = message;
                return RedirectToAction("DanhSachDonDat");
            }

            don.TrangThai = "DaHuy";

            if (don.KhungGio != null)
                don.KhungGio.TrangThai = "Trong";

            foreach (var dv in don.DatSanDichVus)
                if (dv.DichVu != null) dv.DichVu.TonKho += dv.SoLuong;

            await _context.SaveChangesAsync();

            if (don.User != null)
            {
                await _emailService.GuiEmailHuyDon(
                    don.User.Email,
                    don.User.HoTen,
                    don.KhungGio?.SanBong?.TenSan ?? "",
                    don.NgayThiDau.ToString("dd/MM/yyyy"),
                    lyDo: $"Owner từ chối: {lyDo}",
                    soTienHoan: don.TienCoc);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = GetOwnerId(),
                VaiTro = "Owner",
                HanhDong = "TuChoiDon",
                DoiTuong = "DatSan",
                DoiTuongId = datSanId,
                MoTa = $"Từ chối đơn {don.MaXacNhan} — Lý do: {lyDo}"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã từ chối đơn {don.MaXacNhan}. Khách sẽ được hoàn 100% tiền cọc ({don.TienCoc:N0}đ).";
            return RedirectToAction("DanhSachDonDat", new { status = "DaHuy" });
        }

        // ══════════════════════════════════════════════════════════
        // 10. HOÀN CỌC CHỦ ĐỘNG — Khi sân có sự cố (ngập, mất điện...)
        // Chỉ áp dụng cho đơn DaXacNhan — không áp dụng cho đơn hệ thống đã tự xử lý
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> HoanCoc()
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            // Đơn DaXacNhan sắp diễn ra — có thể hủy chủ động
            ViewBag.DonCoThe = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.TrangThai == "DaXacNhan"
                         && d.NgayThiDau >= DateTime.Today)
                .OrderBy(d => d.NgayThiDau)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ThucHienHoanCocSuCo(int datSanId, string lyDo)
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();
            var don = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId)
                                       && d.TrangThai == "DaXacNhan");

            if (don == null)
            {
                TempData["Error"] = "Chỉ hoàn cọc được đơn đang ở trạng thái Đã xác nhận!";
                return RedirectToAction("HoanCoc");
            }

            var (success, message) = await _hoanCocService.ThucHienHoanCocAsync(
                don,
                nguonHuy: "OwnerSuCo",
                vaiTroNguoiKhoiTao: "Owner",
                nguoiKhoiTaoId: GetOwnerId(),
                ghiChu: $"Sự cố: {lyDo}"
            );

            if (!success)
            {
                TempData["Error"] = message;
                return RedirectToAction("HoanCoc");
            }

            don.TrangThai = "DaHuy";
            if (don.KhungGio != null) don.KhungGio.TrangThai = "Trong";

            foreach (var dv in don.DatSanDichVus)
                if (dv.DichVu != null) dv.DichVu.TonKho += dv.SoLuong;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = GetOwnerId(),
                VaiTro = "Owner",
                HanhDong = "HoanCocSuCo",
                DoiTuong = "DatSan",
                DoiTuongId = datSanId,
                MoTa = $"Hoàn cọc do sự cố đơn {don.MaXacNhan} — Lý do: {lyDo}"
            });

            await _context.SaveChangesAsync();

            if (don.User != null)
            {
                await _emailService.GuiEmailHuyDon(
                    don.User.Email,
                    don.User.HoTen,
                    don.KhungGio?.SanBong?.TenSan ?? "",
                    don.NgayThiDau.ToString("dd/MM/yyyy"),
                    lyDo: $"Sân có sự cố: {lyDo}",
                    soTienHoan: don.TienCoc);
            }

            TempData["Success"] = $"Đã hủy đơn {don.MaXacNhan} và hoàn 100% cọc ({don.TienCoc:N0}đ) cho khách.";
            return RedirectToAction("HoanCoc");
        }

        [HttpPost]
        public async Task<IActionResult> ThucHienHoanCocKhieuNai(int datSanId, decimal soTienHoan, string ghiChu)
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();
            var don = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.DatSanDichVus).ThenInclude(dv => dv.DichVu)
                .FirstOrDefaultAsync(d => d.Id == datSanId
                                       && sanIds.Contains(d.KhungGio.SanBongId)
                                       && d.TrangThai == "DaXacNhan");

            if (don == null)
            {
                TempData["Error"] = "Chỉ hoàn cọc được đơn đang ở trạng thái Đã xác nhận!";
                return RedirectToAction("HoanCoc");
            }

            var (success, message) = await _hoanCocService.ThucHienHoanCocAsync(
                don,
                nguonHuy: "OwnerKhieuNai",
                vaiTroNguoiKhoiTao: "Owner",
                nguoiKhoiTaoId: GetOwnerId(),
                soTienHoanTuyChon: soTienHoan,
                ghiChu: ghiChu
            );

            if (!success)
            {
                TempData["Error"] = message;
                return RedirectToAction("HoanCoc");
            }

            don.TrangThai = "DaHuy";
            if (don.KhungGio != null) don.KhungGio.TrangThai = "Trong";

            foreach (var dv in don.DatSanDichVus)
                if (dv.DichVu != null) dv.DichVu.TonKho += dv.SoLuong;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = GetOwnerId(),
                VaiTro = "Owner",
                HanhDong = "HoanCocKhieuNai",
                DoiTuong = "DatSan",
                DoiTuongId = datSanId,
                MoTa = $"Hoàn cọc do khiếu nại đơn {don.MaXacNhan} — Số tiền: {soTienHoan:N0}đ — Ghi chú: {ghiChu}"
            });

            await _context.SaveChangesAsync();

            if (don.User != null)
            {
                await _emailService.GuiEmailHuyDon(
                    don.User.Email,
                    don.User.HoTen,
                    don.KhungGio?.SanBong?.TenSan ?? "",
                    don.NgayThiDau.ToString("dd/MM/yyyy"),
                    lyDo: $"Hoàn cọc do khiếu nại: {ghiChu}",
                    soTienHoan: soTienHoan);
            }

            TempData["Success"] = $"Đã hủy đơn {don.MaXacNhan} và hoàn {soTienHoan:N0}đ cho khách.";
            return RedirectToAction("HoanCoc");
        }

        // ══════════════════════════════════════════════════════════
        // HO SO
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> HoSo(string tab = "tongquan")
        {
            var ownerId = GetOwnerId();
            var user = await _context.Users.FindAsync(ownerId);
            var sanList = await SanCuaToi().ToListAsync();
            var sanIds = sanList.Select(s => s.Id).ToList();

            // Doanh thu thực từ DB
            var allDon = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && (d.TrangThai == "HoanThanh" || d.TrangThai == "DaHuy"
                          || d.TrangThai == "DaXacNhan" || d.TrangThai == "DangSuDung"))
                .ToListAsync();

            decimal tongDT = allDon.Sum(d => d.TongTien > 0 ? d.TongTien : d.TienCoc);
            decimal tongPhi = 0; // Tính theo tỷ lệ hoa hồng nếu cần

            // Lấp đầy sân
            var lapDayData = sanList.Select(s => new {
                TenSan = s.TenSan,
                Tong = s.KhungGios?.Count ?? 0,
                DaDat = s.KhungGios?.Count(k => k.TrangThai == "DaDat") ?? 0
            }).Cast<dynamic>().ToList();

            // Kho cảnh báo
            var khoCanhBao = await _context.DichVus
                .Include(d => d.SanBong)
                .Where(d => sanIds.Contains(d.SanBongId)
                         && d.IsActive && d.TonKho < 20)
                .OrderBy(d => d.TonKho).ToListAsync();

            // Đơn chờ duyệt
            var donChoDuyet = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.TrangThai == "ChoDuyet")
                .OrderBy(d => d.ThoiGianTao).ToListAsync();

            var donGanDay = await _context.DatSans
                .Include(d => d.User)
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && d.ThoiGianTao >= DateTime.Now.AddDays(-7))
                .OrderByDescending(d => d.ThoiGianTao).Take(20).ToListAsync();

            ViewBag.SanList = sanList;
            ViewBag.StaffList = await _context.Users
                .Where(u => u.OwnerIdCuaStaff == ownerId).ToListAsync();
            ViewBag.DonChoDuyet = donChoDuyet;
            ViewBag.DonGanDay = donGanDay;
            ViewBag.LapDayData = lapDayData;
            ViewBag.KhoCanhBao = khoCanhBao;
            ViewBag.SoSan = sanList.Count;
            ViewBag.SoSanDaDuyet = sanList.Count(s => s.TrangThaiDuyet == "DaDuyet");
            ViewBag.SoStaff = await _context.Users.CountAsync(u => u.OwnerIdCuaStaff == ownerId);
            ViewBag.TongDoanhThu = (double)tongDT;
            ViewBag.TongPhiHoaHong = (double)tongPhi;
            ViewBag.Tab = tab;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatThongTin(string hoTen, string? soDienThoai)
        {
            var ownerId = GetOwnerId();
            var user = await _context.Users.FindAsync(ownerId);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                TempData["Error"] = "Họ tên không được để trống.";
                return RedirectToAction("HoSo", new { tab = "thongtin" });
            }

            user.HoTen = hoTen.Trim();
            if (user.SoDienThoai != soDienThoai?.Trim())
            {
                user.SoDienThoai = soDienThoai?.Trim();
                user.DaXacThucSdt = false;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật thông tin cá nhân!";
            return RedirectToAction("HoSo", new { tab = "thongtin" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiMatKhau(string matKhauCu, string matKhauMoi, string xacNhanMatKhau)
        {
            var ownerId = GetOwnerId();
            var user = await _context.Users.FindAsync(ownerId);
            if (user == null) return NotFound();

            if (string.IsNullOrEmpty(matKhauCu) || !BCrypt.Net.BCrypt.Verify(matKhauCu, user.MatKhau))
            {
                TempData["Error"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction("HoSo", new { tab = "baomat" });
            }
            if (matKhauMoi != xacNhanMatKhau)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("HoSo", new { tab = "baomat" });
            }
            if (string.IsNullOrEmpty(matKhauMoi) || matKhauMoi.Length < 6)
            {
                TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return RedirectToAction("HoSo", new { tab = "baomat" });
            }

            user.MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhauMoi);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("HoSo", new { tab = "baomat" });
        }

        // ══════════════════════════════════════════════════════════
        // UC068 — Voucher sân (Owner tạo voucher riêng)
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> VoucherSan()
        {
            var ownerId = GetOwnerId();
            var vouchers = await _context.Vouchers
                .Include(v => v.SanBong)
                .Where(v => v.OwnerId == ownerId)
                .OrderByDescending(v => v.NgayTao)
                .ToListAsync();

            ViewBag.SanCuaToi = await SanCuaToi()
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .Select(s => new { s.Id, s.TenSan })
                .ToListAsync();

            return View(vouchers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoVoucherSan(
            string tenVoucher, string maVoucher, string? moTa,
            string loaiGiam, decimal giaTriGiam, decimal? giamToiDa,
            int soNgayHieuLuc, string loaiPhatHanh,
            int? sanBongId, int? soLuotConLai, int diemCanDoi)
        {
            var ownerId = GetOwnerId();

            if (string.IsNullOrWhiteSpace(tenVoucher) || string.IsNullOrWhiteSpace(maVoucher))
            { TempData["Error"] = "Tên và mã voucher không được để trống."; return RedirectToAction("VoucherSan"); }

            maVoucher = maVoucher.Trim().ToUpper();

            if (await _context.Vouchers.AnyAsync(v => v.MaVoucher == maVoucher))
            { TempData["Error"] = $"Mã voucher \"{maVoucher}\" đã tồn tại."; return RedirectToAction("VoucherSan"); }

            if (sanBongId.HasValue)
            {
                var hopLe = await SanCuaToi().AnyAsync(s => s.Id == sanBongId.Value);
                if (!hopLe)
                { TempData["Error"] = "Sân không thuộc quyền quản lý."; return RedirectToAction("VoucherSan"); }
            }

            if (loaiGiam == "PhanTram" && (giaTriGiam <= 0 || giaTriGiam > 100))
            { TempData["Error"] = "Phần trăm giảm phải trong khoảng 1-100."; return RedirectToAction("VoucherSan"); }
            if (loaiGiam == "SoTien" && giaTriGiam <= 0)
            { TempData["Error"] = "Số tiền giảm phải lớn hơn 0."; return RedirectToAction("VoucherSan"); }

            var v = new Voucher
            {
                MaVoucher = maVoucher,
                TenVoucher = tenVoucher.Trim(),
                MoTa = moTa?.Trim(),
                LoaiGiam = loaiGiam == "SoTien" ? "SoTien" : "PhanTram",
                GiaTriGiam = giaTriGiam,
                GiamToiDa = giamToiDa,
                SoNgayHieuLuc = soNgayHieuLuc > 0 ? soNgayHieuLuc : 30,
                IsActive = true,
                NgayTao = DateTime.Now,
                OwnerId = ownerId,
                SanBongId = sanBongId,
                LoaiPhatHanh = loaiPhatHanh == "DoiDiem" ? "DoiDiem" : "CongKhai",
                SoLuotConLai = soLuotConLai,
                DiemCanDoi = loaiPhatHanh == "DoiDiem" ? diemCanDoi : 0
            };
            _context.Vouchers.Add(v);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã tạo voucher \"{tenVoucher}\" với mã {maVoucher}.";
            return RedirectToAction("VoucherSan");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleVoucher(int id)
        {
            var ownerId = GetOwnerId();
            var v = await _context.Vouchers.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (v == null) return NotFound();
            v.IsActive = !v.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = v.IsActive ? "Đã bật voucher." : "Đã tắt voucher.";
            return RedirectToAction("VoucherSan");
        }

        // ══════════════════════════════════════════════════════════
        // UC069 — Yêu cầu đổi giờ
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> YeuCauDoiGio(string tab = "ChoPheDuyet")
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            var list = await _context.YeuCauDoiGios
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.KhungGioMoi).ThenInclude(k => k.SanBong)
                .Where(y => sanIds.Contains(y.DatSan.KhungGio.SanBongId) && y.TrangThai == tab)
                .OrderByDescending(y => y.NgayTao)
                .ToListAsync();

            ViewBag.Tab = tab;
            ViewBag.SoChoDuyet = await _context.YeuCauDoiGios
                .CountAsync(y => sanIds.Contains(y.DatSan.KhungGio.SanBongId) && y.TrangThai == "ChoPheDuyet");
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyDoiGio(int id, string quyetDinh, string? ghiChu)
        {
            var ownerId = GetOwnerId();
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            var yc = await _context.YeuCauDoiGios
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.KhungGioMoi)
                .FirstOrDefaultAsync(y => y.Id == id && sanIds.Contains(y.DatSan.KhungGio.SanBongId));

            if (yc == null) return NotFound();
            if (yc.TrangThai != "ChoPheDuyet")
            { TempData["Error"] = "Yêu cầu đã được xử lý."; return RedirectToAction("YeuCauDoiGio"); }

            if (quyetDinh == "Duyet")
            {
                // Khung giờ mới phải còn trống
                if (yc.KhungGioMoi.TrangThai != "Trong")
                { TempData["Error"] = "Khung giờ mới đã không còn trống. Vui lòng từ chối yêu cầu này."; return RedirectToAction("YeuCauDoiGio"); }

                // Khung mới phải cùng sân
                if (yc.KhungGioMoi.SanBongId != yc.DatSan.KhungGio.SanBongId)
                { TempData["Error"] = "Khung giờ mới không thuộc cùng sân. Hãy dùng đổi sân thay vì đổi giờ."; return RedirectToAction("YeuCauDoiGio"); }

                var khungCu = yc.DatSan.KhungGio;
                khungCu.TrangThai = "Trong";

                yc.KhungGioMoi.TrangThai = "DaDat";
                yc.DatSan.KhungGioId = yc.KhungGioMoi.Id;
                yc.DatSan.NgayThiDau = yc.NgayThiDauMoi;

                yc.TrangThai = "DaDuyet";
                yc.NgayXuLy = DateTime.Now;
                yc.NguoiXuLyId = ownerId;
                yc.GhiChuXuLy = ghiChu;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner",
                    HanhDong = "DuyetDoiGio", DoiTuong = "YeuCauDoiGio", DoiTuongId = yc.Id,
                    MoTa = $"DatSan {yc.DatSanId}: khung {khungCu.Id} → {yc.KhungGioMoi.Id}",
                    ThoiGian = DateTime.Now
                });
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.GuiEmailAsync(
                        yc.DatSan.User.Email, yc.DatSan.User.HoTen,
                        "Yêu cầu đổi giờ đã được duyệt",
                        $"<p>Xin chào {yc.DatSan.User.HoTen},</p>" +
                        $"<p>Yêu cầu đổi giờ đặt sân #{yc.DatSan.MaXacNhan} đã được duyệt.</p>" +
                        $"<p>Khung giờ mới: {yc.KhungGioMoi.GioBatDau}–{yc.KhungGioMoi.GioKetThuc} ngày {yc.NgayThiDauMoi:dd/MM/yyyy}.</p>");
                }
                catch { /* email lỗi không block flow */ }

                TempData["Success"] = "Đã duyệt yêu cầu đổi giờ.";
            }
            else
            {
                yc.TrangThai = "TuChoi";
                yc.NgayXuLy = DateTime.Now;
                yc.NguoiXuLyId = ownerId;
                yc.GhiChuXuLy = ghiChu;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner",
                    HanhDong = "TuChoiDoiGio", DoiTuong = "YeuCauDoiGio", DoiTuongId = yc.Id,
                    MoTa = ghiChu ?? "", ThoiGian = DateTime.Now
                });
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.GuiEmailAsync(
                        yc.DatSan.User.Email, yc.DatSan.User.HoTen,
                        "Yêu cầu đổi giờ bị từ chối",
                        $"<p>Xin chào {yc.DatSan.User.HoTen},</p>" +
                        $"<p>Yêu cầu đổi giờ đặt sân #{yc.DatSan.MaXacNhan} đã bị từ chối.</p>" +
                        $"<p>Lý do: {ghiChu ?? "(không có)"}</p>");
                }
                catch { }

                TempData["Success"] = "Đã từ chối yêu cầu.";
            }
            return RedirectToAction("YeuCauDoiGio");
        }

        // ══════════════════════════════════════════════════════════
        // UC070 — Yêu cầu đổi sân
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> YeuCauDoiSan(string tab = "ChoPheDuyet")
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            var list = await _context.YeuCauDoiSans
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.KhungGioMoi).ThenInclude(k => k.SanBong)
                .Where(y => sanIds.Contains(y.DatSan.KhungGio.SanBongId) && y.TrangThai == tab)
                .OrderByDescending(y => y.NgayTao)
                .ToListAsync();

            ViewBag.Tab = tab;
            ViewBag.SoChoDuyet = await _context.YeuCauDoiSans
                .CountAsync(y => sanIds.Contains(y.DatSan.KhungGio.SanBongId) && y.TrangThai == "ChoPheDuyet");
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyDoiSan(int id, string quyetDinh, string? ghiChu)
        {
            var ownerId = GetOwnerId();
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            var yc = await _context.YeuCauDoiSans
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.DatSan).ThenInclude(d => d.KhungGio)
                .Include(y => y.KhungGioMoi).ThenInclude(k => k.SanBong)
                .FirstOrDefaultAsync(y => y.Id == id && sanIds.Contains(y.DatSan.KhungGio.SanBongId));

            if (yc == null) return NotFound();
            if (yc.TrangThai != "ChoPheDuyet")
            { TempData["Error"] = "Yêu cầu đã được xử lý."; return RedirectToAction("YeuCauDoiSan"); }

            if (quyetDinh == "Duyet")
            {
                if (yc.KhungGioMoi.TrangThai != "Trong")
                { TempData["Error"] = "Khung giờ tại sân mới đã không còn trống."; return RedirectToAction("YeuCauDoiSan"); }

                // Sân mới cũng phải thuộc Owner để có quyền duyệt
                if (!sanIds.Contains(yc.KhungGioMoi.SanBongId))
                { TempData["Error"] = "Sân mới không thuộc quyền quản lý của bạn."; return RedirectToAction("YeuCauDoiSan"); }

                var khungCu = yc.DatSan.KhungGio;
                khungCu.TrangThai = "Trong";

                yc.KhungGioMoi.TrangThai = "DaDat";
                yc.DatSan.KhungGioId = yc.KhungGioMoi.Id;
                yc.DatSan.NgayThiDau = yc.NgayThiDauMoi;

                yc.TrangThai = "DaDuyet";
                yc.NgayXuLy = DateTime.Now;
                yc.NguoiXuLyId = ownerId;
                yc.GhiChuXuLy = ghiChu;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner",
                    HanhDong = "DuyetDoiSan", DoiTuong = "YeuCauDoiSan", DoiTuongId = yc.Id,
                    MoTa = $"DatSan {yc.DatSanId}: san {khungCu.SanBongId} → {yc.KhungGioMoi.SanBongId}",
                    ThoiGian = DateTime.Now
                });
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.GuiEmailAsync(
                        yc.DatSan.User.Email, yc.DatSan.User.HoTen,
                        "Yêu cầu đổi sân đã được duyệt",
                        $"<p>Xin chào {yc.DatSan.User.HoTen},</p>" +
                        $"<p>Đơn #{yc.DatSan.MaXacNhan} đã được chuyển sang sân \"{yc.KhungGioMoi.SanBong.TenSan}\".</p>" +
                        $"<p>Khung giờ: {yc.KhungGioMoi.GioBatDau}–{yc.KhungGioMoi.GioKetThuc} ngày {yc.NgayThiDauMoi:dd/MM/yyyy}.</p>");
                }
                catch { }

                TempData["Success"] = "Đã duyệt yêu cầu đổi sân.";
            }
            else
            {
                yc.TrangThai = "TuChoi";
                yc.NgayXuLy = DateTime.Now;
                yc.NguoiXuLyId = ownerId;
                yc.GhiChuXuLy = ghiChu;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner",
                    HanhDong = "TuChoiDoiSan", DoiTuong = "YeuCauDoiSan", DoiTuongId = yc.Id,
                    MoTa = ghiChu ?? "", ThoiGian = DateTime.Now
                });
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.GuiEmailAsync(
                        yc.DatSan.User.Email, yc.DatSan.User.HoTen,
                        "Yêu cầu đổi sân bị từ chối",
                        $"<p>Yêu cầu đổi sân của đơn #{yc.DatSan.MaXacNhan} đã bị từ chối.</p>" +
                        $"<p>Lý do: {ghiChu ?? "(không có)"}</p>");
                }
                catch { }

                TempData["Success"] = "Đã từ chối yêu cầu.";
            }
            return RedirectToAction("YeuCauDoiSan");
        }

        // ══════════════════════════════════════════════════════════
        // UC071 — Chuyển nhượng đặt sân
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> ChuyenNhuong(string tab = "ChoPheDuyet")
        {
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            var list = await _context.ChuyenNhuongDatSans
                .Include(c => c.DatSan).ThenInclude(d => d.User)
                .Include(c => c.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(c => c.NguoiChuyen)
                .Include(c => c.NguoiNhan)
                .Where(c => sanIds.Contains(c.DatSan.KhungGio.SanBongId) && c.TrangThai == tab)
                .OrderByDescending(c => c.NgayTao)
                .ToListAsync();

            ViewBag.Tab = tab;
            ViewBag.SoChoDuyet = await _context.ChuyenNhuongDatSans
                .CountAsync(c => sanIds.Contains(c.DatSan.KhungGio.SanBongId) && c.TrangThai == "ChoPheDuyet");
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyChuyenNhuong(int id, string quyetDinh, string? ghiChu)
        {
            var ownerId = GetOwnerId();
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            var cn = await _context.ChuyenNhuongDatSans
                .Include(c => c.DatSan).ThenInclude(d => d.User)
                .Include(c => c.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(c => c.NguoiChuyen)
                .FirstOrDefaultAsync(c => c.Id == id && sanIds.Contains(c.DatSan.KhungGio.SanBongId));

            if (cn == null) return NotFound();
            if (cn.TrangThai != "ChoPheDuyet")
            { TempData["Error"] = "Yêu cầu đã được xử lý."; return RedirectToAction("ChuyenNhuong"); }

            if (quyetDinh == "Duyet")
            {
                // Lookup người nhận theo email hoặc SĐT
                User? nguoiNhan = null;
                if (!string.IsNullOrWhiteSpace(cn.EmailNguoiNhan))
                    nguoiNhan = await _context.Users.FirstOrDefaultAsync(u => u.Email == cn.EmailNguoiNhan && u.IsActive);
                if (nguoiNhan == null && !string.IsNullOrWhiteSpace(cn.SdtNguoiNhan))
                    nguoiNhan = await _context.Users.FirstOrDefaultAsync(u => u.SoDienThoai == cn.SdtNguoiNhan && u.IsActive);

                if (nguoiNhan == null)
                { TempData["Error"] = "Không tìm thấy tài khoản người nhận. Hãy từ chối yêu cầu này."; return RedirectToAction("ChuyenNhuong"); }
                if (nguoiNhan.Id == cn.NguoiChuyenId)
                { TempData["Error"] = "Người nhận trùng với người chuyển."; return RedirectToAction("ChuyenNhuong"); }

                var nguoiChuyenTruoc = cn.DatSan.User;
                cn.DatSan.UserId = nguoiNhan.Id;
                cn.NguoiNhanId = nguoiNhan.Id;

                cn.TrangThai = "DaDuyet";
                cn.NgayXuLy = DateTime.Now;
                cn.NguoiXuLyOwnerId = ownerId;
                cn.GhiChuXuLy = ghiChu;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner",
                    HanhDong = "DuyetChuyenNhuong", DoiTuong = "ChuyenNhuongDatSan", DoiTuongId = cn.Id,
                    MoTa = $"DatSan {cn.DatSanId}: user {nguoiChuyenTruoc.Id} → {nguoiNhan.Id}",
                    ThoiGian = DateTime.Now
                });
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.GuiEmailAsync(
                        nguoiChuyenTruoc.Email, nguoiChuyenTruoc.HoTen,
                        "Chuyển nhượng đặt sân thành công",
                        $"<p>Đơn #{cn.DatSan.MaXacNhan} đã được chuyển sang {nguoiNhan.HoTen} ({nguoiNhan.Email}).</p>");
                    await _emailService.GuiEmailAsync(
                        nguoiNhan.Email, nguoiNhan.HoTen,
                        "Bạn vừa nhận một đơn đặt sân",
                        $"<p>Xin chào {nguoiNhan.HoTen},</p>" +
                        $"<p>Bạn vừa nhận đơn đặt sân #{cn.DatSan.MaXacNhan} từ {nguoiChuyenTruoc.HoTen}.</p>" +
                        $"<p>Sân: {cn.DatSan.KhungGio.SanBong.TenSan}, ngày {cn.DatSan.NgayThiDau:dd/MM/yyyy}.</p>");
                }
                catch { }

                TempData["Success"] = "Đã duyệt chuyển nhượng.";
            }
            else
            {
                cn.TrangThai = "TuChoi";
                cn.NgayXuLy = DateTime.Now;
                cn.NguoiXuLyOwnerId = ownerId;
                cn.GhiChuXuLy = ghiChu;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner",
                    HanhDong = "TuChoiChuyenNhuong", DoiTuong = "ChuyenNhuongDatSan", DoiTuongId = cn.Id,
                    MoTa = ghiChu ?? "", ThoiGian = DateTime.Now
                });
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.GuiEmailAsync(
                        cn.NguoiChuyen.Email, cn.NguoiChuyen.HoTen,
                        "Yêu cầu chuyển nhượng bị từ chối",
                        $"<p>Yêu cầu chuyển nhượng đơn #{cn.DatSan.MaXacNhan} đã bị từ chối.</p>" +
                        $"<p>Lý do: {ghiChu ?? "(không có)"}</p>");
                }
                catch { }

                TempData["Success"] = "Đã từ chối yêu cầu.";
            }
            return RedirectToAction("ChuyenNhuong");
        }
    }
}