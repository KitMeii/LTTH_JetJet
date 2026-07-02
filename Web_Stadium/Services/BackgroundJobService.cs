using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;

namespace Web_Stadium.Services
{
    public class BackgroundJobService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundJobService> _logger;

        // Chạy mỗi 5 phút
        private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

        public BackgroundJobService(
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundJobService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔄 BackgroundJobService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<SanBongContext>();
                    var email = scope.ServiceProvider.GetRequiredService<EmailService>();
                    var hoanCocService = scope.ServiceProvider.GetRequiredService<HoanCocService>();

                    await XuLyHuyDonQuaHan(context, email, hoanCocService);
                    await XuLyNoShow(context, email, hoanCocService);
                    await GuiNhacLich24h(context, email);
                    await GuiNhacLich1h(context, email);
                    await GuiMoiDanhGia(context, email);
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
        private async Task XuLyHuyDonQuaHan(SanBongContext context, EmailService email, HoanCocService hoanCocService)
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
                await hoanCocService.ThucHienHoanCocAsync(
                    don,
                    nguonHuy: "SystemTimeout",
                    vaiTroNguoiKhoiTao: "System",
                    nguoiKhoiTaoId: null,
                    ghiChu: "Owner không xác nhận trong 6 giờ — hệ thống tự động hủy"
                );

                don.TrangThai = "DaHuy";
                if (don.KhungGio != null)
                    don.KhungGio.TrangThai = "Trong";

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
        private async Task XuLyNoShow(SanBongContext context, EmailService email, HoanCocService hoanCocService)
        {
            var now = DateTime.Now;

            var donNoShow = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.TrangThai == "DaXacNhan")
                .ToListAsync();

            foreach (var don in donNoShow)
            {
                if (don.KhungGio == null) continue;

                var gioBD = don.KhungGio.GioBatDau.ToTimeSpan();
                var gioBatDauTran = don.NgayThiDau.Date.Add(gioBD);

                if (now >= gioBatDauTran.AddMinutes(30))
                {
                    await hoanCocService.ThucHienHoanCocAsync(
                        don,
                        nguonHuy: "SystemNoShow",
                        vaiTroNguoiKhoiTao: "System",
                        nguoiKhoiTaoId: null,
                        soTienHoanTuyChon: 0m,
                        ghiChu: "Tự động ghi nhận No-show sau 30 phút"
                    );

                    don.TrangThai = "DaHuy";
                    don.LoaiSuCo = "NoShow";
                    don.GhiChuSuCo = "Tự động ghi nhận No-show sau 30 phút";
                    if (don.KhungGio != null)
                        don.KhungGio.TrangThai = "Trong";

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
            var tu24h = now.AddHours(23).AddMinutes(45); // window 23h45 → 24h15
            var den24h = now.AddHours(24).AddMinutes(15);

            var donSapToi = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.TrangThai == "DaXacNhan")
                .ToListAsync();

            foreach (var don in donSapToi)
            {
                if (don.KhungGio == null || don.User == null) continue;

                var gioBD = don.KhungGio.GioBatDau.ToTimeSpan();
                var gioBatDauTran = don.NgayThiDau.Date.Add(gioBD);

                if (gioBatDauTran >= tu24h && gioBatDauTran <= den24h)
                {
                    // Kiểm tra chưa gửi (dùng GhiChuSuCo tạm, hoặc dùng AuditLog)
                    var daGui = await context.AuditLogs
                        .AnyAsync(a => a.DoiTuongId == don.Id
                                    && a.HanhDong == "NhacLich24h");
                    if (daGui) continue;

                    var tenSan = don.KhungGio.SanBong?.TenSan ?? "";
                    var diaChi = don.KhungGio.SanBong?.DiaChi + ", " + don.KhungGio.SanBong?.Quan;
                    var khungGio = $"{don.KhungGio.GioBatDau:HH\\:mm} – {don.KhungGio.GioKetThuc:HH\\:mm}";

                    await email.GuiEmailNhacLich(
                        don.User.Email, don.User.HoTen,
                        tenSan, diaChi, khungGio,
                        don.NgayThiDau.ToString("dd/MM/yyyy"),
                        don.MaXacNhan, la24h: true);

                    // Ghi log để không gửi lại
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
                .Where(d => d.TrangThai == "DaXacNhan")
                .ToListAsync();

            foreach (var don in donSapToi)
            {
                if (don.KhungGio == null || don.User == null) continue;

                var gioBD = don.KhungGio.GioBatDau.ToTimeSpan();
                var gioBatDauTran = don.NgayThiDau.Date.Add(gioBD);

                if (gioBatDauTran >= tu1h && gioBatDauTran <= den1h)
                {
                    var daGui = await context.AuditLogs
                        .AnyAsync(a => a.DoiTuongId == don.Id
                                    && a.HanhDong == "NhacLich1h");
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
            // Tìm đơn HoanThanh trong khoảng 30-60 phút trước (để không bỏ sót)
            var tuGio = now.AddMinutes(-60);
            var denGio = now.AddMinutes(-30);

            // Lấy đơn HoanThanh nhưng chưa được gửi email mời đánh giá
            var donHoanThanh = await context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(d => d.User)
                .Where(d => d.TrangThai == "HoanThanh")
                .ToListAsync();

            foreach (var don in donHoanThanh)
            {
                if (don.User == null || don.KhungGio?.SanBong == null) continue;

                // Kiểm tra đã gửi chưa
                var daGui = await context.AuditLogs
                    .AnyAsync(a => a.DoiTuongId == don.Id
                                && a.HanhDong == "GuiMoiDanhGia");
                if (daGui) continue;

                // Tính thời điểm kết thúc trận (giờ kết thúc của ngày thi đấu)
                var gioKT = don.KhungGio.GioKetThuc.ToTimeSpan();
                var gioKetThucTran = don.NgayThiDau.Date.Add(gioKT);

                // Chỉ gửi khi đã qua 30 phút kể từ kết thúc trận
                if (now < gioKetThucTran.AddMinutes(30)) continue;

                // Kiểm tra user chưa đánh giá đơn này
                var daDanhGia = await context.DanhGias
                    .AnyAsync(dg => dg.DatSanId == don.Id);
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
    }
}