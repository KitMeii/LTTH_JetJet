using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Hubs;

namespace Web_Stadium.Services
{
    public class BackgroundJobService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundJobService> _logger;
        private readonly IHubContext<SanBongHub> _hubContext;

        private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        private int _tickCount = 0;

        public BackgroundJobService(
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundJobService> logger,
            IHubContext<SanBongHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔄 BackgroundJobService started");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _tickCount++;
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<SanBongContext>();
                    var email = scope.ServiceProvider.GetRequiredService<EmailService>();

                    await XuLyHuyDonQuaHan(context, email);
                    await XuLyNoShow(context, email);
                    await GuiNhacLich24h(context, email);
                    await GuiNhacLich1h(context, email);
                    await GuiMoiDanhGia(context, email);
                    await GuiNhacCheckIn30phut(context, email);

                    // Chạy mỗi 15 phút (mỗi 3 tick × 5 phút)
                    if (_tickCount % 3 == 0)
                        await XuLyHuyKhongCheckIn(context, email);
                }
                catch (Exception ex)
                {
                    _logger.LogError("❌ BackgroundJob error: {Msg}", ex.Message);
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        // ══════════════════════════════════════════════════════════
        // JOB 1: Hủy đơn ChoDuyet quá 6 giờ Owner không phản hồi
        // ══════════════════════════════════════════════════════════
        private async Task XuLyHuyDonQuaHan(SanBongContext context, EmailService email)
        {
            var nguongHuy = DateTime.Now.AddHours(-6);

            var donQuaHan = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.TrangThai == "ChoDuyet"
                         && d.ThoiGianTao <= nguongHuy)
                .ToListAsync();

            foreach (var don in donQuaHan)
            {
                don.TrangThai = "DaHuy";
                if (don.KhungGio != null)
                {
                    don.KhungGio.TrangThai = "Trong";
                    don.KhungGio.ThoiGianHetGiuCho = null;
                }

                _logger.LogInformation("⏰ Tự hủy đơn {Ma} — quá 6h Owner không duyệt", don.MaXacNhan);

                if (don.User != null && !string.IsNullOrEmpty(don.User.Email))
                {
                    var tenSan = don.KhungGio?.SanBong?.TenSan ?? "Không rõ";
                    await email.GuiEmailHuyDon(
                        don.User.Email,
                        don.User.HoTen,
                        tenSan,
                        don.NgayThiDau.ToString("dd/MM/yyyy"),
                        lyDo: "Owner không xác nhận trong 6 giờ — hệ thống tự động hủy",
                        soTienHoan: don.TienCoc
                    );
                }
            }

            if (donQuaHan.Any())
                await context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // JOB 2: No-show — đơn DaXacNhan quá 30 phút chưa check-in
        // ══════════════════════════════════════════════════════════
        private async Task XuLyNoShow(SanBongContext context, EmailService email)
        {
            var now = DateTime.Now;

            var donNoShow = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.TrangThai == "DaXacNhan"
                         && d.NgayThiDau.Date == DateTime.Today)
                .ToListAsync();

            foreach (var don in donNoShow)
            {
                if (don.KhungGio == null) continue;

                var gioBD = don.KhungGio.GioBatDau.ToTimeSpan();
                var gioBatDauTran = don.NgayThiDau.Date.Add(gioBD);

                if (now >= gioBatDauTran.AddMinutes(30))
                {
                    don.TrangThai = "DaHuy";
                    don.LoaiSuCo = "NoShow";
                    don.GhiChuSuCo = "Tự động ghi nhận No-show sau 30 phút";
                    if (don.KhungGio != null)
                    {
                        don.KhungGio.TrangThai = "Trong";
                        don.KhungGio.ThoiGianHetGiuCho = null;
                    }

                    _logger.LogInformation("🚫 No-show: đơn {Ma}", don.MaXacNhan);
                }
            }

            if (donNoShow.Any(d => d.TrangThai == "DaHuy"))
                await context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // JOB 3: Nhắc lịch trước 24 giờ
        // ══════════════════════════════════════════════════════════
        private async Task GuiNhacLich24h(SanBongContext context, EmailService email)
        {
            var now = DateTime.Now;
            var tu24h = now.AddHours(23).AddMinutes(45);
            var den24h = now.AddHours(24).AddMinutes(15);

            var donSapToi = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.TrangThai == "DaXacNhan"
                         && d.NgayThiDau.Date >= tu24h.Date
                         && d.NgayThiDau.Date <= den24h.Date)
                .ToListAsync();

            foreach (var don in donSapToi)
            {
                if (don.KhungGio == null || don.User == null) continue;

                var gioBD = don.KhungGio.GioBatDau.ToTimeSpan();
                var gioBatDauTran = don.NgayThiDau.Date.Add(gioBD);

                if (gioBatDauTran >= tu24h && gioBatDauTran <= den24h)
                {
                    var daGui = await context.AuditLogs
                        .AnyAsync(a => a.DoiTuongId == don.Id && a.HanhDong == "NhacLich24h");
                    if (daGui) continue;

                    var tenSan = don.KhungGio.SanBong?.TenSan ?? "";
                    var diaChi = don.KhungGio.SanBong?.DiaChi + ", " + don.KhungGio.SanBong?.Quan;
                    var khungGio = $"{don.KhungGio.GioBatDau:HH\\:mm} – {don.KhungGio.GioKetThuc:HH\\:mm}";

                    await email.GuiEmailNhacLich(
                        don.User.Email, don.User.HoTen,
                        tenSan, diaChi, khungGio,
                        don.NgayThiDau.ToString("dd/MM/yyyy"),
                        don.MaXacNhan, la24h: true);

                    context.AuditLogs.Add(new AuditLog
                    {
                        UserId = don.UserId,
                        VaiTro = "System",
                        HanhDong = "NhacLich24h",
                        DoiTuong = "DatSan",
                        DoiTuongId = don.Id,
                        MoTa = $"Đã gửi nhắc 24h: {tenSan}"
                    });

                    _logger.LogInformation("📧 Nhắc 24h → {Email} | {San}", don.User.Email, tenSan);
                }
            }

            await context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // JOB 4: Nhắc lịch trước 1 giờ
        // ══════════════════════════════════════════════════════════
        private async Task GuiNhacLich1h(SanBongContext context, EmailService email)
        {
            var now = DateTime.Now;
            var tu1h = now.AddMinutes(45);
            var den1h = now.AddMinutes(75);

            var donSapToi = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.TrangThai == "DaXacNhan"
                         && d.NgayThiDau.Date == DateTime.Today)
                .ToListAsync();

            foreach (var don in donSapToi)
            {
                if (don.KhungGio == null || don.User == null) continue;

                var gioBD = don.KhungGio.GioBatDau.ToTimeSpan();
                var gioBatDauTran = don.NgayThiDau.Date.Add(gioBD);

                if (gioBatDauTran >= tu1h && gioBatDauTran <= den1h)
                {
                    var daGui = await context.AuditLogs
                        .AnyAsync(a => a.DoiTuongId == don.Id && a.HanhDong == "NhacLich1h");
                    if (daGui) continue;

                    var tenSan = don.KhungGio.SanBong?.TenSan ?? "";
                    var diaChi = don.KhungGio.SanBong?.DiaChi + ", " + don.KhungGio.SanBong?.Quan;
                    var khungGio = $"{don.KhungGio.GioBatDau:HH\\:mm} – {don.KhungGio.GioKetThuc:HH\\:mm}";

                    await email.GuiEmailNhacLich(
                        don.User.Email, don.User.HoTen,
                        tenSan, diaChi, khungGio,
                        don.NgayThiDau.ToString("dd/MM/yyyy"),
                        don.MaXacNhan, la24h: false);

                    context.AuditLogs.Add(new AuditLog
                    {
                        UserId = don.UserId,
                        VaiTro = "System",
                        HanhDong = "NhacLich1h",
                        DoiTuong = "DatSan",
                        DoiTuongId = don.Id,
                        MoTa = $"Đã gửi nhắc 1h: {tenSan}"
                    });

                    _logger.LogInformation("📧 Nhắc 1h → {Email} | {San}", don.User.Email, tenSan);
                }
            }

            await context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // JOB 5: Gửi email mời đánh giá 30 phút sau HoanThanh
        // ══════════════════════════════════════════════════════════
        private async Task GuiMoiDanhGia(SanBongContext context, EmailService email)
        {
            var now = DateTime.Now;

            var nguongHoanThanh = now.AddDays(-7);
            var donHoanThanh = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.TrangThai == "HoanThanh"
                         && d.NgayThiDau >= nguongHoanThanh)
                .ToListAsync();

            foreach (var don in donHoanThanh)
            {
                if (don.User == null || don.KhungGio?.SanBong == null) continue;

                var daGui = await context.AuditLogs
                    .AnyAsync(a => a.DoiTuongId == don.Id && a.HanhDong == "GuiMoiDanhGia");
                if (daGui) continue;

                var gioKT = don.KhungGio.GioKetThuc.ToTimeSpan();
                var gioKetThucTran = don.NgayThiDau.Date.Add(gioKT);

                if (now < gioKetThucTran.AddMinutes(30)) continue;

                var daDanhGia = await context.DanhGias.AnyAsync(dg => dg.DatSanId == don.Id);
                if (daDanhGia) continue;

                await email.GuiEmailMoiDanhGia(
                    don.User.Email, don.User.HoTen,
                    don.KhungGio.SanBong.TenSan,
                    don.KhungGio.SanBong.Id,
                    don.Id);

                context.AuditLogs.Add(new AuditLog
                {
                    UserId = don.UserId,
                    VaiTro = "System",
                    HanhDong = "GuiMoiDanhGia",
                    DoiTuong = "DatSan",
                    DoiTuongId = don.Id,
                    MoTa = $"Đã gửi email mời đánh giá: {don.KhungGio.SanBong.TenSan}"
                });

                _logger.LogInformation("📧 Mời đánh giá → {Email} | đơn {Ma}", don.User.Email, don.MaXacNhan);
            }

            await context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // JOB 6: Tự hủy đơn DaXacNhan chưa check-in sau 2 tiếng
        //        + SignalR mở lại khung giờ + email thông báo khách
        //        (chạy mỗi 15 phút)
        // ══════════════════════════════════════════════════════════
        private async Task XuLyHuyKhongCheckIn(SanBongContext context, EmailService email)
        {
            var donHomNay = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d =>
                    d.TrangThai == "DaXacNhan" &&
                    d.StaffCheckInId == null &&
                    d.NgayThiDau.Date == DateTime.Today)
                .ToListAsync();

            // Lọc trong C#: giờ bắt đầu + 2 tiếng < giờ hiện tại
            var now = DateTime.Now;
            var donCanHuy = donHomNay.Where(d =>
            {
                if (d.KhungGio == null) return false;
                var gioBatDau = d.NgayThiDau.Date.Add(d.KhungGio.GioBatDau.ToTimeSpan());
                return gioBatDau.AddHours(2) < now;
            }).ToList();

            foreach (var don in donCanHuy)
            {
                don.TrangThai = "DaHuy";
                don.GhiChuSuCo = "Tự động hủy: khách không check-in sau 2 tiếng";

                if (don.KhungGio != null)
                {
                    don.KhungGio.TrangThai = "Trong";
                    don.KhungGio.ThoiGianHetGiuCho = null;

                    // Cập nhật realtime cho tất cả client đang xem sân
                    await _hubContext.Clients
                        .Group($"san_{don.KhungGio.SanBongId}")
                        .SendAsync("CapNhatKhungGio", new
                        {
                            khungGioId = don.KhungGio.Id,
                            trangThai = "Trong",
                            hetHan = (DateTime?)null
                        });
                }

                _logger.LogInformation(
                    "⏰ Tự hủy đơn {Ma} — không check-in sau 2 tiếng | Sân: {San}",
                    don.MaXacNhan, don.KhungGio?.SanBong?.TenSan ?? "?");

                // Gửi email thông báo cho khách
                if (don.User?.Email != null && don.KhungGio != null)
                {
                    var tenSan = don.KhungGio.SanBong?.TenSan ?? "";
                    await email.GuiEmailHuyDonTuDong(
                        don.User.Email,
                        don.User.HoTen,
                        tenSan,
                        don.NgayThiDau,
                        don.KhungGio.GioBatDau.ToString(@"hh\:mm"),
                        don.KhungGio.GioKetThuc.ToString(@"hh\:mm"),
                        don.MaXacNhan);
                }
            }

            if (donCanHuy.Any())
                await context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // JOB 7: Nhắc check-in trước 30 phút
        //        (chạy mỗi 5 phút)
        // ══════════════════════════════════════════════════════════
        private async Task GuiNhacCheckIn30phut(SanBongContext context, EmailService email)
        {
            var now = DateTime.Now;
            var sap25phut = now.AddMinutes(25);
            var sap35phut = now.AddMinutes(35);

            var donHomNay = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d =>
                    d.TrangThai == "DaXacNhan" &&
                    d.NgayThiDau.Date == DateTime.Today)
                .ToListAsync();

            // Lọc: giờ bắt đầu nằm trong khoảng 25–35 phút tới
            var donSapDen = donHomNay.Where(d =>
            {
                if (d.KhungGio == null) return false;
                var gioBatDau = d.NgayThiDau.Date.Add(d.KhungGio.GioBatDau.ToTimeSpan());
                return gioBatDau >= sap25phut && gioBatDau <= sap35phut;
            }).ToList();

            foreach (var don in donSapDen)
            {
                if (don.User == null || don.KhungGio == null) continue;

                // Kiểm tra chưa gửi nhắc (dùng AuditLog)
                var daGui = await context.AuditLogs
                    .AnyAsync(a => a.DoiTuongId == don.Id && a.HanhDong == "NhacCheckIn30phut");
                if (daGui) continue;

                var tenSan = don.KhungGio.SanBong?.TenSan ?? "";
                await email.GuiEmailNhacCheckIn(
                    don.User.Email,
                    don.User.HoTen,
                    tenSan,
                    don.NgayThiDau,
                    don.KhungGio.GioBatDau.ToString(@"hh\:mm"),
                    don.MaXacNhan);

                context.AuditLogs.Add(new AuditLog
                {
                    UserId = don.UserId,
                    VaiTro = "System",
                    HanhDong = "NhacCheckIn30phut",
                    DoiTuong = "DatSan",
                    DoiTuongId = don.Id,
                    MoTa = $"Đã gửi nhắc check-in 30 phút: {tenSan}"
                });

                _logger.LogInformation(
                    "📧 Nhắc check-in 30 phút → {Email} | đơn {Ma}",
                    don.User.Email, don.MaXacNhan);
            }

            await context.SaveChangesAsync();
        }
    }
}
