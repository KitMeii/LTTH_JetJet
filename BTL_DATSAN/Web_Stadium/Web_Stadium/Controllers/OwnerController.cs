using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;
using Web_Stadium.Hubs;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Owner")]
    public class OwnerController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        private readonly IHubContext<SanBongHub> _hub;
        private readonly CloudinaryService _cloudinaryService;

        public OwnerController(SanBongContext context, IConfiguration config,
            EmailService emailService, IHubContext<SanBongHub> hub,
            CloudinaryService cloudinaryService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
            _hub = hub;
            _cloudinaryService = cloudinaryService;
        }

        // Helper lấy OwnerId từ JWT
        private int GetOwnerId() => TokenHelper.LayUserId(Request, _config)!.Value;

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

            // Badge đổi giờ — yêu cầu đã qua Staff, chờ Owner duyệt
            ViewBag.SoYeuCauDoiGio = await _context.YeuCauDoiGios
                .Include(y => y.DatSan).ThenInclude(d => d.KhungGio)
                .CountAsync(y => sanIds.Contains(y.DatSan.KhungGio.SanBongId)
                              && y.TrangThai == "ChoXuLy"
                              && y.StaffXuLyId != null);

            // Badge đổi sân — yêu cầu mới đang chờ Owner xử lý
            ViewBag.SoYeuCauDoiSan = await _context.YeuCauDoiSans
                .Include(y => y.SanMoi)
                .CountAsync(y => y.SanMoi.OwnerId == ownerId
                              && y.TrangThai == "ChoXuLy");

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

        public async Task<IActionResult> XemHopDong(int sanId)
        {
            var san = await _context.SanBongs
                .FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();
            if (!san.DaKyHopDong || string.IsNullOrEmpty(san.NoiDungHopDong))
            {
                TempData["Error"] = "Sân chưa có nội dung hợp đồng! Vui lòng điền đầy đủ thông tin sân trước.";
                return RedirectToAction("DanhSachSan");
            }
            ViewBag.San = san;
            return View(san);
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

            var soHopDong = $"PH-{ngayKy:yyyyMMdd}-{(owner?.Id ?? 0):D4}";
            var ngayKetThuc = ngayKy.AddYears(1);
            var tyLeHHPct = (tyLeHH * 100).ToString("F0");
            var tyLeBenAPct = tyLeHHPct;
            var tyLeBenBPct = (100 - decimal.Parse(tyLeHHPct)).ToString("F0");

            var hopDong = $@"CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM
Độc lập – Tự do – Hạnh phúc

HỢP ĐỒNG HỢP TÁC KINH DOANH
Số:........./HĐHTKD

Căn cứ vào Bộ Luật Dân Sự số 91/2015/QH13 ngày 24 tháng 11 năm 2015;
Căn cứ vào Luật Thương Mại số 36/2005/QH11 ngày 14 tháng 06 năm 2005;
Căn cứ vào tình hình thực tế và nhu cầu hợp tác của Hai bên;
Dựa trên tinh thần trung thực và thiện chí hợp tác;

Chúng tôi gồm có:

BÊN A (Đơn vị vận hành nền tảng):
  Tên đơn vị    : Công ty TNHH PitchHub Việt Nam
  Địa chỉ       : Thành phố Hồ Chí Minh, Việt Nam
  Đại diện      : Giám đốc điều hành PitchHub
  Email         : contact@pitchhub.vn
  Điện thoại    : 1800-PITCH
  (Sau đây gọi tắt là Bên A)

VÀ

BÊN B (Chủ sân bóng):
  Họ và tên     : {owner?.HoTen ?? "..."}
  Địa chỉ       : {diaChi}, {quan}, {thanhPho}
  Điện thoại    : {owner?.SoDienThoai ?? "..."}
  Email         : {owner?.Email ?? "..."}
  (Sau đây gọi tắt là Bên B)

Cùng thỏa thuận ký hợp đồng hợp tác kinh doanh với những điều khoản sau:

Điều 1. MỤC TIÊU VÀ PHẠM VI HỢP TÁC

1.1. Mục tiêu hợp tác
Bên A và Bên B nhất trí hợp tác kinh doanh, điều hành và chia sẻ lợi nhuận 
từ hoạt động cho thuê sân bóng thông qua nền tảng trực tuyến PitchHub.

1.2. Phạm vi hợp tác
Bên B đăng ký đưa cơ sở thể thao ""{tenSan}"" lên hệ thống PitchHub:
  - Địa chỉ sân : {diaChi}, {quan}, {thanhPho}
  - Loại sân    : Sân {loaiSan} người
  - Loại cỏ     : {loaiCo}
  - Khu vực     : {tenVung}

Phạm vi Bên A: Vận hành nền tảng đặt sân, tiếp thị, hỗ trợ thanh toán.
Phạm vi Bên B: Quản lý, vận hành cơ sở vật chất và dịch vụ tại sân.

Điều 2. THỜI HẠN HỢP ĐỒNG

Thời hạn hợp đồng: 1 năm (12 tháng) kể từ ngày ký.
  - Bắt đầu : {ngayKy:dd/MM/yyyy}
  - Kết thúc : {ngayKetThuc:dd/MM/yyyy}

Gia hạn hợp đồng: Hết thời hạn trên, hai bên có thể thỏa thuận gia hạn 
thêm 12 tháng hoặc ký hợp đồng mới. Nếu không có thông báo chấm dứt 
trước 30 ngày, hợp đồng tự động gia hạn.

Điều 3. PHÍ HOA HỒNG VÀ PHÂN CHIA LỢI NHUẬN

3.1. Tỷ lệ hoa hồng
Căn cứ khu vực {quan} thuộc vùng ""{tenVung}"", tỷ lệ áp dụng:
  - Bên A (PitchHub) được hưởng : {tyLeBenAPct}% trên doanh thu thực phát sinh
  - Bên B (Chủ sân)  được hưởng : {tyLeBenBPct}% trên doanh thu thực phát sinh

3.2. Phương thức tính
  - Khách hủy đặt sân : Hoa hồng Bên A = Tiền cọc đã thu × {tyLeBenAPct}%
                        Bên B nhận = Tiền cọc - Hoa hồng Bên A
  - Khách sử dụng đủ  : Hoa hồng Bên A = Tổng tiền thuê sân × {tyLeBenAPct}%
                        Bên B nhận = Tổng tiền thuê sân - Hoa hồng Bên A

3.3. Thanh toán
Định kỳ hàng tháng, chậm nhất ngày 10 tháng kế tiếp qua chuyển khoản ngân hàng.

Điều 4. CÁC NGUYÊN TẮC TÀI CHÍNH

4.1. Hai bên tuân thủ nguyên tắc tài chính kế toán theo quy định pháp luật 
     Cộng hoà xã hội chủ nghĩa Việt Nam.
4.2. Mọi khoản thu chi đều được ghi chép rõ ràng, đầy đủ, xác thực.
4.3. Hệ thống PitchHub ghi nhận tự động toàn bộ giao dịch đặt sân.

Điều 5. QUYỀN VÀ NGHĨA VỤ CỦA BÊN A

5.1. Quyền của Bên A
  - Được hưởng {tyLeBenAPct}% hoa hồng theo Điều 3.
  - Tạm ngưng hoặc gỡ sân khỏi hệ thống nếu Bên B vi phạm các điều khoản.
  - Điều chỉnh tỷ lệ hoa hồng sau khi thông báo trước 30 ngày.

5.2. Nghĩa vụ của Bên A
  - Cung cấp nền tảng đặt sân ổn định, hỗ trợ kỹ thuật 24/7.
  - Quảng bá sân đến khách hàng trên toàn hệ thống.
  - Thanh toán phần doanh thu sau khi trừ hoa hồng đúng hạn quy định.
  - Bảo mật thông tin kinh doanh của Bên B.

Điều 6. QUYỀN VÀ NGHĨA VỤ CỦA BÊN B

6.1. Quyền của Bên B
  - Được hưởng {tyLeBenBPct}% doanh thu sau khi trừ hoa hồng.
  - Tự quản lý lịch đặt sân, khung giờ và giá thuê trên hệ thống.
  - Yêu cầu hỗ trợ kỹ thuật từ Bên A khi cần thiết.

6.2. Nghĩa vụ của Bên B
  - Cung cấp thông tin sân trung thực, đầy đủ và cập nhật kịp thời.
  - Đảm bảo chất lượng cơ sở vật chất phù hợp với mô tả trên hệ thống.
  - Không hủy đặt sân của khách hàng quá 3 lần/tháng không có lý do.
  - Thực hiện đúng chính sách hoàn cọc theo quy định của PitchHub.
  - Không tự ý liên hệ khách hàng để giao dịch ngoài hệ thống.

Điều 7. ĐIỀU KHOẢN CHUNG

7.1. Hợp đồng chịu sự điều chỉnh của pháp luật Cộng hoà xã hội chủ nghĩa Việt Nam.
7.2. Bên vi phạm gây thiệt hại phải bồi thường toàn bộ và chịu phạt 8% 
     giá trị hợp đồng bị vi phạm (trừ trường hợp bất khả kháng).
7.3. Trong quá trình thực hiện, bên có khó khăn phải thông báo cho bên 
     kia trong vòng 30 ngày.
7.4. Mọi sửa đổi, bổ sung phải bằng văn bản và có chữ ký của hai bên.
7.5. Tranh chấp được giải quyết qua thương lượng, hoà giải; nếu không 
     thành sẽ giải quyết tại Toà án có thẩm quyền.

Điều 8. HIỆU LỰC HỢP ĐỒNG

8.1. Hợp đồng chấm dứt khi hết thời hạn tại Điều 2 hoặc theo quy định pháp luật.
     Khi kết thúc, hai bên lập biên bản thanh lý hợp đồng.
8.2. Hợp đồng được lập điện tử 02 (hai) bản bằng tiếng Việt, mỗi bên 
     giữ 01 (một) bản có giá trị pháp lý như nhau.
8.3. Hợp đồng có hiệu lực kể từ ngày được Admin PitchHub phê duyệt.

                    {thanhPho}, ngày {ngayKy:dd} tháng {ngayKy:MM} năm {ngayKy:yyyy}

        ĐẠI DIỆN BÊN A                    ĐẠI DIỆN BÊN B
   (Ký, ghi rõ họ tên, đóng dấu)      (Ký, ghi rõ họ tên)

   Công ty TNHH PitchHub                  {owner?.HoTen ?? "..."}
   Giám đốc điều hành                     Chủ sân {tenSan}";

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
        public async Task<IActionResult> SuaSan(int sanId)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();

            ViewBag.TyLeMap = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.VungKhuVuc != null)
                .ToDictionaryAsync(q => q.TenQuan, q => q.VungKhuVuc!.TyLeHoaHong);

            ViewBag.LoaiSans = await _context.DanhMucLoaiSans.Where(l => l.IsActive).ToListAsync();
            ViewBag.LoaiCos = await _context.DanhMucLoaiCos.Where(l => l.IsActive).ToListAsync();
            return View(san);
        }

        [HttpPost]
        public async Task<IActionResult> SuaSan(int sanId, string moTa, decimal tyLeCoc, bool isHidden)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();
            san.MoTa = moTa;
            san.TyLeCoc = tyLeCoc;
            san.IsHidden = isHidden;
            await _context.SaveChangesAsync();

            // AuditLog hành động nhạy cảm
            _context.AuditLogs.Add(new Web_Stadium.EFCore.AuditLog
            {
                UserId = GetOwnerId(),
                VaiTro = "Owner",
                HanhDong = "SuaSan",
                DoiTuong = "SanBong",
                DoiTuongId = sanId,
                MoTa = $"Sửa sân {san.TenSan} — IsHidden:{isHidden}, TyLeCoc:{tyLeCoc:P0}"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật thông tin \"{san.TenSan}\"!";
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
        public async Task<IActionResult> ThemKhungGio(int sanId, TimeSpan gioBatDau,
            TimeSpan gioKetThuc, decimal gia, decimal giaGioVang,
            decimal giaCuoiTuan, string loaiNgay)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();

            var kg = new KhungGio
            {
                SanBongId = sanId,
                GioBatDau = TimeOnly.FromTimeSpan(gioBatDau),
                GioKetThuc = TimeOnly.FromTimeSpan(gioKetThuc),
                Gia = gia,
                GiaGioVang = giaGioVang,
                GiaCuoiTuan = giaCuoiTuan,
                LoaiNgay = loaiNgay,
                TrangThai = "Trong"
            };
            _context.KhungGios.Add(kg);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã thêm khung giờ {gioBatDau:hh\\:mm}–{gioKetThuc:hh\\:mm}!";
            return RedirectToAction("KhungGio", new { sanId });
        }

        [HttpPost]
        public async Task<IActionResult> SuaKhungGio(int id, decimal gia,
            decimal giaGioVang, decimal giaCuoiTuan)
        {
            var kg = await _context.KhungGios
                .Include(k => k.SanBong)
                .FirstOrDefaultAsync(k => k.Id == id && k.SanBong.OwnerId == GetOwnerId());
            if (kg == null) return NotFound();
            var giaOld = kg.Gia;
            kg.Gia = gia; kg.GiaGioVang = giaGioVang; kg.GiaCuoiTuan = giaCuoiTuan;
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new Web_Stadium.EFCore.AuditLog
            {
                UserId = GetOwnerId(),
                VaiTro = "Owner",
                HanhDong = "SuaGia",
                DoiTuong = "KhungGio",
                DoiTuongId = id,
                MoTa = $"Sửa giá KhungGio #{id}: {giaOld:N0} → {gia:N0}đ"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật bảng giá!";
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

        [HttpPost]
        public async Task<IActionResult> KhoaStaff(int staffId, bool isActive)
        {
            var ownerId = GetOwnerId();
            var staff = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == staffId && u.OwnerIdCuaStaff == ownerId);
            if (staff == null) return NotFound();
            staff.IsActive = isActive;
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
        public async Task<IActionResult> CapNhatDichVu(int dichVuId, decimal gia,
            int tonKho, bool isActive)
        {
            var dv = await _context.DichVus
                .Include(d => d.SanBong)
                .FirstOrDefaultAsync(d => d.Id == dichVuId && d.SanBong.OwnerId == GetOwnerId());
            if (dv == null) return NotFound();
            dv.Gia = gia; dv.TonKho = tonKho; dv.IsActive = isActive;
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

        // ══════════════════════════════════════════════════════════
        // 8. BÁO CÁO CHI NHÁNH
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> BaoCao(
            string loai = "thang", int? nam = null, int? thang = null)
        {
            var now = DateTime.Now;
            nam ??= now.Year;
            thang ??= now.Month;
            ViewBag.Loai = loai; ViewBag.Nam = nam; ViewBag.Thang = thang;

            var ownerId = GetOwnerId();
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            IQueryable<DatSan> baseQ = _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Include(d => d.StaffCheckIn)
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId)
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                          || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy"));

            var tyLeMap = await LayTyLeMapAsync();

            DateTime batDau, ketThuc;
            var data = new List<object>();
            string tieuDe;

            switch (loai)
            {
                case "ngay":
                    batDau = new DateTime(nam.Value, thang.Value, 1);
                    ketThuc = batDau.AddMonths(1);
                    tieuDe = $"Theo ngày — Tháng {thang}/{nam}";
                    for (int ng = 1; ng <= DateTime.DaysInMonth(nam.Value, thang.Value); ng++)
                    {
                        var bd = new DateTime(nam.Value, thang.Value, ng);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < bd.AddDays(1)).ToListAsync();
                        data.Add(BuildOwnerPoint($"{ng}/{thang}", rows, tyLeMap));
                    }
                    break;

                case "tuan":
                    batDau = new DateTime(nam.Value, thang.Value, 1);
                    ketThuc = batDau.AddMonths(1);
                    tieuDe = $"Theo tuần — Tháng {thang}/{nam}";
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
                    batDau = new DateTime(nam.Value, 1, 1);
                    ketThuc = new DateTime(nam.Value + 1, 1, 1);
                    tieuDe = $"Theo quý — Năm {nam}";
                    for (int q = 1; q <= 4; q++)
                    {
                        var bd = new DateTime(nam.Value, (q - 1) * 3 + 1, 1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < bd.AddMonths(3)).ToListAsync();
                        data.Add(BuildOwnerPoint($"Q{q}/{nam}", rows, tyLeMap));
                    }
                    break;

                case "nam":
                    batDau = new DateTime(now.Year - 4, 1, 1);
                    ketThuc = new DateTime(now.Year + 1, 1, 1);
                    tieuDe = "Theo năm — 5 năm gần nhất";
                    for (int y = now.Year - 4; y <= now.Year; y++)
                    {
                        var bd = new DateTime(y, 1, 1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < new DateTime(y + 1, 1, 1)).ToListAsync();
                        data.Add(BuildOwnerPoint($"{y}", rows, tyLeMap));
                    }
                    break;

                default: // thang
                    batDau = new DateTime(nam.Value, 1, 1);
                    ketThuc = new DateTime(nam.Value + 1, 1, 1);
                    tieuDe = $"Theo tháng — Năm {nam}";
                    for (int m = 1; m <= 12; m++)
                    {
                        var bd = new DateTime(nam.Value, m, 1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < bd.AddMonths(1)).ToListAsync();
                        data.Add(BuildOwnerPoint($"T{m}", rows, tyLeMap));
                    }
                    break;
            }

            ViewBag.Data = data;
            ViewBag.TieuDe = tieuDe;
            ViewBag.TongThuThuan = (double)data.Sum(d => (double)((dynamic)d).thuThuan);
            ViewBag.TongLuot = (int)data.Sum(d => (double)((dynamic)d).soLuot);

            // Tỷ lệ lấp đầy từng sân
            ViewBag.TyLeLapDay = await SanCuaToi()
                .Include(s => s.KhungGios)
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .Select(s => new
                {
                    Ten = s.TenSan,
                    Tong = s.KhungGios.Count,
                    DaDat = s.KhungGios.Count(k => k.TrangThai == "DaDat")
                }).ToListAsync();

            // Hiệu suất Staff
            var allRows = await baseQ
                .Where(d => d.ThoiGianTao >= batDau && d.ThoiGianTao < ketThuc)
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
        // QUAN LY ANH SAN
        // ══════════════════════════════════════════════════════════

        // GET /Owner/QuanLyAnh?sanId=1
        public async Task<IActionResult> QuanLyAnh(int sanId)
        {
            var san = await SanCuaToi()
                .Include(s => s.AnhSanBongs.Where(a => a.IsActive).OrderBy(a => a.ThuTu))
                .FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();
            ViewBag.San = san;
            return View(san.AnhSanBongs.ToList());
        }

        // POST /Owner/ThemAnhURL — them anh tu URL
        [HttpPost]
        public async Task<IActionResult> ThemAnhURL(int sanId, string duongDan, string? moTa)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();

            if (string.IsNullOrWhiteSpace(duongDan))
            {
                TempData["Error"] = "Vui lòng nhập đường dẫn ảnh!";
                return RedirectToAction("QuanLyAnh", new { sanId });
            }

            // Kiem tra URL hop le
            if (!Uri.TryCreate(duongDan, UriKind.Absolute, out _) &&
                !duongDan.StartsWith("/"))
            {
                TempData["Error"] = "Đường dẫn không hợp lệ. Vui lòng nhập URL đầy đủ (https://...) hoặc đường dẫn /images/...";
                return RedirectToAction("QuanLyAnh", new { sanId });
            }

            // So thu tu
            var thuTu = await _context.AnhSanBongs
                .Where(a => a.SanBongId == sanId && a.IsActive)
                .MaxAsync(a => (int?)a.ThuTu) ?? 0;

            _context.AnhSanBongs.Add(new AnhSanBong
            {
                SanBongId = sanId,
                DuongDan = duongDan.Trim(),
                LoaiAnh = duongDan.StartsWith("http") ? "URL" : "Upload",
                MoTa = moTa,
                ThuTu = thuTu + 1,
                NgayThem = DateTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm ảnh thành công!";
            return RedirectToAction("QuanLyAnh", new { sanId });
        }

        // POST /Owner/ThemAnhFile — upload file tu may
        [HttpPost]
        public async Task<IActionResult> ThemAnhFile(int sanId, IFormFile file, string? moTa)
        {
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file ảnh!";
                return RedirectToAction("QuanLyAnh", new { sanId });
            }

            // Chi cho phep anh
            var ext = Path.GetExtension(file.FileName).ToLower();
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExt.Contains(ext))
            {
                TempData["Error"] = "Chỉ chấp nhận file .jpg, .jpeg, .png, .webp";
                return RedirectToAction("QuanLyAnh", new { sanId });
            }

            // Gioi han 10MB
            if (file.Length > 10 * 1024 * 1024)
            {
                TempData["Error"] = "File quá lớn. Tối đa 10MB.";
                return RedirectToAction("QuanLyAnh", new { sanId });
            }

            // Upload lên Cloudinary
            var url = await _cloudinaryService.UploadAnhAsync(file, $"san/{sanId}");
            if (url == null)
            {
                TempData["Error"] = "Upload ảnh thất bại. Vui lòng thử lại.";
                return RedirectToAction("QuanLyAnh", new { sanId });
            }

            // So thu tu
            var thuTu = await _context.AnhSanBongs
                .Where(a => a.SanBongId == sanId && a.IsActive)
                .MaxAsync(a => (int?)a.ThuTu) ?? 0;

            _context.AnhSanBongs.Add(new AnhSanBong
            {
                SanBongId = sanId,
                DuongDan = url,
                LoaiAnh = "Upload",
                MoTa = moTa,
                ThuTu = thuTu + 1,
                NgayThem = DateTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã upload ảnh '{file.FileName}' thành công!";
            return RedirectToAction("QuanLyAnh", new { sanId });
        }

        // POST /Owner/XoaAnh
        [HttpPost]
        public async Task<IActionResult> XoaAnh(int anhId, int sanId)
        {
            var anh = await _context.AnhSanBongs
                .Include(a => a.SanBong)
                .FirstOrDefaultAsync(a => a.Id == anhId && a.SanBong.OwnerId == GetOwnerId());
            if (anh == null) return NotFound();

            // Xóa ảnh trên Cloudinary nếu là file upload
            if (anh.LoaiAnh == "Upload" && !string.IsNullOrEmpty(anh.DuongDan))
            {
                var publicId = _cloudinaryService.LayPublicId(anh.DuongDan);
                if (publicId != null)
                    _ = Task.Run(() => _cloudinaryService.XoaAnhAsync(publicId));
            }

            anh.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa ảnh!";
            return RedirectToAction("QuanLyAnh", new { sanId });
        }

        // POST /Owner/DoiThuTuAnh
        [HttpPost]
        public async Task<IActionResult> DoiThuTuAnh(int sanId, int[] anhIds)
        {
            var anhList = await _context.AnhSanBongs
                .Include(a => a.SanBong)
                .Where(a => a.SanBong.OwnerId == GetOwnerId() && a.SanBongId == sanId)
                .ToListAsync();

            for (int i = 0; i < anhIds.Length; i++)
            {
                var anh = anhList.FirstOrDefault(a => a.Id == anhIds[i]);
                if (anh != null) anh.ThuTu = i + 1;
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
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
                return RedirectToAction("DuyetDon");
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
            return RedirectToAction("DuyetDon");
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
                return RedirectToAction("DuyetDon");
            }

            don.TrangThai = "DaHuy";

            // Giải phóng slot
            if (don.KhungGio != null)
                don.KhungGio.TrangThai = "Trong";

            // Hoàn kho dịch vụ đặt trước (chưa trừ kho vì chưa check-in)
            foreach (var dv in don.DatSanDichVus)
                if (dv.DichVu != null) dv.DichVu.TonKho += dv.SoLuong;

            await _context.SaveChangesAsync();

            // Gửi email thông báo từ chối + hoàn tiền cho khách
            if (don.User != null)
            {
                await _emailService.GuiEmailHuyDon(
                    don.User.Email,
                    don.User.HoTen,
                    don.KhungGio?.SanBong?.TenSan ?? "",
                    don.NgayThiDau.ToString("dd/MM/yyyy"),
                    lyDo: $"Owner từ chối: {lyDo}",
                    soTienHoan: don.TienCoc); // hoàn 100%
            }

            // Ghi AuditLog
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
            return RedirectToAction("DuyetDon");
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
        public async Task<IActionResult> ThucHienHoanCoc(int datSanId, string lyDo)
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

            don.TrangThai = "DaHuy";
            if (don.KhungGio != null) don.KhungGio.TrangThai = "Trong";

            // Hoàn kho (đơn DaXacNhan kho chưa bị trừ — chỉ trừ khi check-in)
            foreach (var dv in don.DatSanDichVus)
                if (dv.DichVu != null) dv.DichVu.TonKho += dv.SoLuong;

            // Ghi AuditLog
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = GetOwnerId(),
                VaiTro = "Owner",
                HanhDong = "HoanCocChuDong",
                DoiTuong = "DatSan",
                DoiTuongId = datSanId,
                MoTa = $"Hoàn cọc chủ động đơn {don.MaXacNhan} — Lý do: {lyDo}"
            });

            await _context.SaveChangesAsync();

            // Gửi email thông báo cho khách
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

        // ══════════════════════════════════════════════════════════
        // YÊU CẦU ĐỔI GIỜ — Owner xem và phê duyệt
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> YeuCauDoiGio()
        {
            var ownerId = GetOwnerId();
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();
            // Chỉ hiện yêu cầu đã qua Staff (StaffXuLyId != null) và vẫn ChoXuLy
            var list = await _context.YeuCauDoiGios
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.KhungGioMoi)
                .Include(y => y.StaffXuLy)
                .Where(y => sanIds.Contains(y.DatSan.KhungGio.SanBongId)
                         && y.TrangThai == "ChoXuLy"
                         && y.StaffXuLyId != null)
                .OrderByDescending(y => y.ThoiGianTao)
                .ToListAsync();
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> XuLyYeuCau(int yeuCauId, string ghiChuOwner, string hanhDong)
        {
            var ownerId = GetOwnerId();
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();
            var yc = await _context.YeuCauDoiGios
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.KhungGioMoi)
                .FirstOrDefaultAsync(y => y.Id == yeuCauId
                    && sanIds.Contains(y.DatSan.KhungGio.SanBongId)
                    && y.TrangThai == "ChoXuLy"
                    && y.StaffXuLyId != null);

            if (yc == null) return NotFound();

            yc.OwnerXuLyId = ownerId;
            yc.GhiChuOwner = ghiChuOwner?.Trim();
            yc.ThoiGianXuLy = DateTime.Now;

            if (hanhDong == "TuChoi")
            {
                yc.TrangThai = "TuChoi";
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner", HanhDong = "TuChoiDoiGio",
                    DoiTuong = "YeuCauDoiGio", DoiTuongId = yc.Id,
                    MoTa = $"Owner từ chối đổi giờ đơn {yc.DatSan.MaXacNhan}"
                });
                await _context.SaveChangesAsync();
                var lyDoTuChoi = string.IsNullOrWhiteSpace(ghiChuOwner) ? "Owner không chấp thuận yêu cầu." : ghiChuOwner;
                _ = Task.Run(() => _emailService.GuiEmailDoiGioTuChoi(
                    yc.DatSan.User!.Email, yc.DatSan.User.HoTen ?? "Khách",
                    yc.DatSan.KhungGio.SanBong?.TenSan ?? "", lyDoTuChoi));
                TempData["Success"] = "Đã từ chối yêu cầu đổi giờ.";
            }
            else // PheDuyet
            {
                // Kiểm tra khung giờ mới vẫn còn Trong
                var kgMoi = await _context.KhungGios.FindAsync(yc.KhungGioMoiId);
                if (kgMoi == null || kgMoi.TrangThai != "Trong")
                {
                    TempData["Error"] = "Khung giờ mới không còn trống! Hãy từ chối và thông báo khách chọn lại.";
                    return RedirectToAction("YeuCauDoiGio");
                }

                var don = yc.DatSan;
                var kgCu = don.KhungGio;
                var sanBongId = kgCu.SanBongId;

                // Cập nhật đơn
                don.KhungGioId = yc.KhungGioMoiId;
                don.NgayThiDau = yc.NgayMoi;

                // Giải phóng khung giờ cũ
                kgCu.TrangThai = "Trong";
                kgCu.ThoiGianHetGiuCho = null;

                // Khóa khung giờ mới
                kgMoi.TrangThai = "DaDat";

                yc.TrangThai = "DaPheDuyet";

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner", HanhDong = "PheDuyetDoiGio",
                    DoiTuong = "YeuCauDoiGio", DoiTuongId = yc.Id,
                    MoTa = $"Phê duyệt đổi giờ đơn {don.MaXacNhan} → KhungGio #{yc.KhungGioMoiId}"
                });
                await _context.SaveChangesAsync();

                // SignalR broadcast cả 2 khung giờ
                await _hub.Clients.Group($"san_{sanBongId}")
                    .SendAsync("CapNhatKhungGio", new { khungGioId = kgCu.Id, trangThai = "Trong", hetHan = (DateTime?)null });
                await _hub.Clients.Group($"san_{sanBongId}")
                    .SendAsync("CapNhatKhungGio", new { khungGioId = kgMoi.Id, trangThai = "DaDat", hetHan = (DateTime?)null });

                // Email xác nhận cho khách
                var gioMoi = $"{kgMoi.GioBatDau:hh\\:mm} – {kgMoi.GioKetThuc:hh\\:mm}";
                _ = Task.Run(() => _emailService.GuiEmailDoiGioPheDuyet(
                    don.User!.Email, don.User.HoTen ?? "Khách",
                    kgCu.SanBong?.TenSan ?? "", gioMoi,
                    yc.NgayMoi.ToString("dd/MM/yyyy"), don.MaXacNhan));

                TempData["Success"] = "Đã phê duyệt! Khung giờ đã được đổi và khách nhận được email xác nhận.";
            }

            return RedirectToAction("YeuCauDoiGio");
        }

        // ══════════════════════════════════════════════════════════
        // YÊU CẦU ĐỔI SÂN — Owner xem và phê duyệt
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> YeuCauDoiSan()
        {
            var ownerId = GetOwnerId();
            var list = await _context.YeuCauDoiSans
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.SanMoi)
                .Include(y => y.KhungGioMoi)
                .Where(y => y.SanMoi.OwnerId == ownerId && y.TrangThai == "ChoXuLy")
                .OrderByDescending(y => y.ThoiGianTao)
                .ToListAsync();
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> XuLyDoiSan(int yeuCauId, string? ghiChuOwner, string hanhDong)
        {
            var ownerId = GetOwnerId();
            var yc = await _context.YeuCauDoiSans
                .Include(y => y.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(y => y.DatSan).ThenInclude(d => d.User)
                .Include(y => y.SanMoi)
                .Include(y => y.KhungGioMoi)
                .FirstOrDefaultAsync(y => y.Id == yeuCauId
                    && y.SanMoi.OwnerId == ownerId
                    && y.TrangThai == "ChoXuLy");

            if (yc == null) return NotFound();

            yc.OwnerXuLyId = ownerId;
            yc.GhiChuOwner = ghiChuOwner?.Trim();
            yc.ThoiGianXuLy = DateTime.Now;

            if (hanhDong == "TuChoi")
            {
                yc.TrangThai = "TuChoi";
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner", HanhDong = "TuChoiDoiSan",
                    DoiTuong = "YeuCauDoiSan", DoiTuongId = yc.Id,
                    MoTa = $"Owner từ chối đổi sân → {yc.SanMoi.TenSan} cho đơn {yc.DatSan.MaXacNhan}"
                });
                await _context.SaveChangesAsync();

                var lyDoTuChoi = string.IsNullOrWhiteSpace(ghiChuOwner) ? "Owner không chấp thuận yêu cầu." : ghiChuOwner;
                _ = Task.Run(() => _emailService.GuiEmailDoiSanTuChoi(
                    yc.DatSan.User!.Email, yc.DatSan.User.HoTen ?? "Khách",
                    yc.SanMoi.TenSan, lyDoTuChoi));
                TempData["Success"] = "Đã từ chối yêu cầu đổi sân.";
            }
            else // PheDuyet
            {
                // Kiểm tra khung giờ mới vẫn còn Trong
                var kgMoi = await _context.KhungGios.FindAsync(yc.KhungGioMoiId);
                if (kgMoi == null || kgMoi.TrangThai != "Trong")
                {
                    TempData["Error"] = "Khung giờ trên sân mới không còn trống! Hãy từ chối và thông báo khách chọn lại.";
                    return RedirectToAction("YeuCauDoiSan");
                }

                var don = yc.DatSan;
                var kgCu = don.KhungGio;
                var sanCuId = kgCu?.SanBongId ?? 0;

                // Cập nhật đơn: đổi sân + khung giờ
                don.KhungGioId = yc.KhungGioMoiId;

                // Giải phóng khung giờ cũ
                if (kgCu != null)
                {
                    kgCu.TrangThai = "Trong";
                    kgCu.ThoiGianHetGiuCho = null;
                }

                // Khóa khung giờ mới
                kgMoi.TrangThai = "DaDat";

                yc.TrangThai = "DaPheDuyet";

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId, VaiTro = "Owner", HanhDong = "PheDuyetDoiSan",
                    DoiTuong = "YeuCauDoiSan", DoiTuongId = yc.Id,
                    MoTa = $"Phê duyệt đổi sân đơn {don.MaXacNhan} → {yc.SanMoi.TenSan}"
                });
                await _context.SaveChangesAsync();

                // SignalR broadcast cả 2 sân
                if (sanCuId > 0)
                    await _hub.Clients.Group($"san_{sanCuId}")
                        .SendAsync("CapNhatKhungGio", new { khungGioId = kgCu!.Id, trangThai = "Trong", hetHan = (DateTime?)null });
                await _hub.Clients.Group($"san_{yc.SanMoiId}")
                    .SendAsync("CapNhatKhungGio", new { khungGioId = kgMoi.Id, trangThai = "DaDat", hetHan = (DateTime?)null });

                // Email xác nhận cho khách
                var gioMoi = $"{kgMoi.GioBatDau:hh\\:mm} – {kgMoi.GioKetThuc:hh\\:mm}";
                _ = Task.Run(() => _emailService.GuiEmailDoiSanPheDuyet(
                    don.User!.Email, don.User.HoTen ?? "Khách",
                    don.KhungGio?.SanBong?.TenSan ?? "",
                    yc.SanMoi.TenSan, gioMoi,
                    yc.NgayThiDau.ToString("dd/MM/yyyy"), don.MaXacNhan, yc.ChenhLechGia));

                TempData["Success"] = "Đã phê duyệt đổi sân! Khách sẽ nhận được email xác nhận.";
            }

            return RedirectToAction("YeuCauDoiSan");
        }

        // ══════════════════════════════════════════════════════════
        // GET /Owner/ChuyenNhuong
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> ChuyenNhuong()
        {
            var ownerId = GetOwnerId();
            var sanIds = await _context.SanBongs
                .Where(s => s.OwnerId == ownerId)
                .Select(s => s.Id)
                .ToListAsync();

            var list = await _context.ChuyenNhuongs
                .Include(c => c.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(c => c.UserA)
                .Include(c => c.UserB)
                .Where(c => c.TrangThai == "ChoOwner"
                         && sanIds.Contains(c.DatSan.KhungGio.SanBongId))
                .OrderByDescending(c => c.ThoiGianXuLy)
                .ToListAsync();

            return View(list);
        }

        // POST /Owner/XuLyChuyenNhuong
        [HttpPost]
        public async Task<IActionResult> XuLyChuyenNhuong(int chuyenNhuongId, string hanhDong, string? ghiChu)
        {
            var ownerId = GetOwnerId();
            var cn = await _context.ChuyenNhuongs
                .Include(c => c.DatSan)
                    .ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(c => c.UserA)
                .Include(c => c.UserB)
                .FirstOrDefaultAsync(c => c.Id == chuyenNhuongId && c.TrangThai == "ChoOwner");

            if (cn == null) return NotFound();

            var san = cn.DatSan?.KhungGio?.SanBong;
            var ngay = cn.DatSan?.NgayThiDau.ToString("dd/MM/yyyy") ?? "";
            var gio = $"{cn.DatSan?.KhungGio?.GioBatDau:HH\\:mm}–{cn.DatSan?.KhungGio?.GioKetThuc:HH\\:mm}";

            cn.OwnerXuLyId = ownerId;
            cn.GhiChuOwner = ghiChu;
            cn.ThoiGianXuLy = DateTime.Now;

            if (hanhDong == "TuChoi")
            {
                cn.TrangThai = "TuChoi";
                await _context.SaveChangesAsync();
                foreach (var (email, ten) in new[] {
                    (cn.UserA?.Email ?? "", cn.UserA?.HoTen ?? ""),
                    (cn.UserB?.Email ?? "", cn.UserB?.HoTen ?? "")
                })
                {
                    if (!string.IsNullOrEmpty(email))
                        _ = Task.Run(() => _emailService.GuiEmailChuyenNhuongTuChoi(
                            email, ten, san?.TenSan ?? "", ngay, gio, ghiChu ?? "Không được chấp thuận"));
                }
                TempData["Error"] = "Đã từ chối yêu cầu chuyển nhượng.";
            }
            else // PheDuyet
            {
                if (cn.UserBId == null)
                {
                    TempData["Error"] = "Không có người nhận.";
                    return RedirectToAction("ChuyenNhuong");
                }

                // Đổi chủ đơn
                var don = cn.DatSan;
                var oldUserId = don.UserId;
                don.UserId = cn.UserBId.Value;

                // Đánh dấu hoàn tất
                cn.TrangThai = "HoanTat";
                cn.DaChuyenNhuong = true;

                // Cộng điểm cho User A (TienCoc / 1000)
                var userA = await _context.Users.FindAsync(cn.UserAId);
                if (userA != null)
                    userA.DiemHienTai += (int)(don.TienCoc / 1000);

                await _context.SaveChangesAsync();

                // AuditLog
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = ownerId,
                    VaiTro = "Owner",
                    HanhDong = "PheDuyetChuyenNhuong",
                    DoiTuong = "ChuyenNhuong",
                    DoiTuongId = cn.Id,
                    MoTa = $"Phê duyệt chuyển nhượng đơn {don.MaXacNhan} từ User {cn.UserAId} → User {cn.UserBId}",
                    ThoiGian = DateTime.Now
                });
                await _context.SaveChangesAsync();

                // Email cả A và B
                _ = Task.Run(() => _emailService.GuiEmailChuyenNhuongHoanTat(
                    cn.UserA?.Email ?? "", cn.UserA?.HoTen ?? "Bên chuyển",
                    cn.UserB?.Email ?? "", cn.UserB?.HoTen ?? "Bên nhận",
                    san?.TenSan ?? "", ngay, gio, don.MaXacNhan));

                TempData["Success"] = "Đã phê duyệt! Đơn đặt sân đã được chuyển nhượng thành công.";
            }

            return RedirectToAction("ChuyenNhuong");
        }

        // ══════════════════════════════════════════════════════════
        // VOUCHER SÂN
        // ══════════════════════════════════════════════════════════

        // GET /Owner/Voucher
        public async Task<IActionResult> Voucher()
        {
            var ownerId = GetOwnerId();
            var sanIds = await SanCuaToi().Select(s => s.Id).ToListAsync();

            var vouchers = await _context.Vouchers
                .Include(v => v.SanBong)
                .Where(v => v.LoaiVoucher == "Owner" && v.OwnerId == ownerId)
                .OrderByDescending(v => v.NgayTao)
                .ToListAsync();

            var sanList = await SanCuaToi()
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .ToListAsync();

            ViewBag.SanList = sanList;
            ViewBag.TongLuotDung = vouchers.Sum(v => v.DaDung);
            ViewBag.TongTienGiam = await _context.DatSans
                .Where(d => sanIds.Contains(d.KhungGio.SanBongId) && d.VoucherSanId != null)
                .SumAsync(d => d.TienGiamSan);

            return View(vouchers);
        }

        // GET /Owner/TaoVoucher
        public async Task<IActionResult> TaoVoucher()
        {
            ViewBag.SanList = await SanCuaToi()
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .ToListAsync();
            return View();
        }

        // POST /Owner/TaoVoucher
        [HttpPost]
        public async Task<IActionResult> TaoVoucher(
            int sanBongId, string tenVoucher, string? moTa,
            string loaiGiam, decimal giaTriGiam, decimal? giamToiDa,
            decimal dieuKienToiThieu, int soLuong,
            DateTime ngayBatDau, DateTime ngayHetHan)
        {
            var ownerId = GetOwnerId();
            var san = await SanCuaToi().FirstOrDefaultAsync(s => s.Id == sanBongId);
            if (san == null)
            { TempData["Error"] = "Sân không hợp lệ."; return RedirectToAction("TaoVoucher"); }

            if (string.IsNullOrWhiteSpace(tenVoucher))
            { TempData["Error"] = "Tên voucher không được để trống."; return RedirectToAction("TaoVoucher"); }

            if (ngayHetHan <= ngayBatDau)
            { TempData["Error"] = "Ngày hết hạn phải sau ngày bắt đầu."; return RedirectToAction("TaoVoucher"); }

            var ma = $"OWN-{sanBongId}-{DateTime.Now:yyyyMMddHHmmss}";
            var v = new Voucher
            {
                MaVoucher = ma,
                TenVoucher = tenVoucher.Trim(),
                MoTa = moTa?.Trim(),
                LoaiGiam = loaiGiam,
                GiaTriGiam = giaTriGiam,
                GiamToiDa = loaiGiam == "PhanTram" ? giamToiDa : null,
                DieuKienToiThieu = dieuKienToiThieu,
                SoLuong = soLuong,
                LoaiVoucher = "Owner",
                SanBongId = sanBongId,
                OwnerId = ownerId,
                NgayBatDau = ngayBatDau,
                NgayHetHan = ngayHetHan,
                IsActive = true,
                NgayTao = DateTime.Now,
                DiemCanDoi = 0,
                SoNgayHieuLuc = 0
            };
            _context.Vouchers.Add(v);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã tạo voucher \"{tenVoucher}\" cho sân {san.TenSan}.";
            return RedirectToAction("Voucher");
        }

        // POST /Owner/KichHoatVoucher/{id}
        [HttpPost]
        public async Task<IActionResult> KichHoatVoucher(int id)
        {
            var ownerId = GetOwnerId();
            var v = await _context.Vouchers.FindAsync(id);
            if (v == null || v.OwnerId != ownerId) return NotFound();
            v.IsActive = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã kích hoạt voucher \"{v.TenVoucher}\".";
            return RedirectToAction("Voucher");
        }

        // POST /Owner/VoHieuVoucher/{id}
        [HttpPost]
        public async Task<IActionResult> VoHieuVoucher(int id)
        {
            var ownerId = GetOwnerId();
            var v = await _context.Vouchers.FindAsync(id);
            if (v == null || v.OwnerId != ownerId) return NotFound();
            v.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã vô hiệu voucher \"{v.TenVoucher}\".";
            return RedirectToAction("Voucher");
        }
    }
}