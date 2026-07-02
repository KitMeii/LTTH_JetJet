using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Web_Stadium.EFCore;
using Web_Stadium.Services;

namespace Web_Stadium.End
{
    /// <summary>
    /// Background Jobs cho Tournament — chạy định kỳ mỗi phút
    /// Job 1: Hủy đăng ký đội quá 15 phút chưa thanh toán
    /// Job 2: Tự động treo giò sau trận Closed
    /// Job 3: Cảnh báo Owner giải sắp bắt đầu
    /// </summary>
    public class TournamentBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TournamentBackgroundService> _logger;

        // Chạy mỗi phút
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        public TournamentBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<TournamentBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TournamentBackgroundService started.");

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<SanBongContext>();
                    var notifService = scope.ServiceProvider
                        .GetRequiredService<TournamentNotificationService>();
                    var suspService = scope.ServiceProvider
                        .GetRequiredService<SuspensionService>();

                    await HuyDangKyQuaHan(context);
                    await TuDongTreoGio(context, suspService);
                    await CanhBaoGiaiSapBatDau(context, notifService);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TournamentBackgroundService error");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        // ══════════════════════════════════════════════════════════
        // Job 1: Hủy đội quá 15 phút chưa thanh toán
        // ══════════════════════════════════════════════════════════
        private async Task HuyDangKyQuaHan(SanBongContext context)
        {
            var deadline = DateTime.Now.AddMinutes(-15);

            var doiQuaHan = await context.DoiBongs
                .Where(d => !d.DaThanhToan
                         && d.ThoiGianTao < deadline
                         && d.GiaiDau.TrangThai == "RegistrationOpen")
                .ToListAsync();

            if (!doiQuaHan.Any()) return;

            context.DoiBongs.RemoveRange(doiQuaHan);
            await context.SaveChangesAsync();

            _logger.LogInformation(
                "TournamentBG: Đã hủy {Count} đội quá hạn thanh toán.",
                doiQuaHan.Count);
        }

        // ══════════════════════════════════════════════════════════
        // Job 2: Tự động treo giò sau các trận vừa Closed
        // ══════════════════════════════════════════════════════════
        private async Task TuDongTreoGio(
            SanBongContext context, SuspensionService suspService)
        {
            // Lấy giải đang Active có trận vừa Closed trong 2 phút qua
            var giaiCaTran = await context.TranDaus
                .Where(t => t.TrangThai == "Closed"
                         && t.GiaiDau.TrangThai == "Active")
                .Select(t => t.GiaiDauId)
                .Distinct()
                .ToListAsync();

            foreach (var giaiId in giaiCaTran)
            {
                await suspService.XuLyTreoGio(giaiId);
            }
        }

        // ══════════════════════════════════════════════════════════
        // Job 3: Gửi email cảnh báo Owner giải sắp bắt đầu (1 ngày trước)
        // ══════════════════════════════════════════════════════════
        private async Task CanhBaoGiaiSapBatDau(
            SanBongContext context,
            TournamentNotificationService notifService)
        {
            var ngayMai = DateTime.Today.AddDays(1);

            // Giải có ngày bắt đầu là ngày mai, đang Active, chưa gửi cảnh báo
            var giaiSapBD = await context.GiaiDaus
                .Include(g => g.Owner)
                .Where(g => g.TrangThai == "Active"
                         && g.NgayBatDau.Date == ngayMai)
                .ToListAsync();

            // Chỉ gửi 1 lần — kiểm tra AuditLog
            foreach (var giai in giaiSapBD)
            {
                var daCanhBao = await context.AuditLogs.AnyAsync(a =>
                    a.DoiTuong == "GiaiDau" &&
                    a.DoiTuongId == giai.Id &&
                    a.HanhDong == "CanhBaoSapBatDau");

                if (daCanhBao) continue;

                // Ghi log để không gửi lại
                context.AuditLogs.Add(new Web_Stadium.EFCore.AuditLog
                {
                    UserId = giai.OwnerId,
                    VaiTro = "System",
                    HanhDong = "CanhBaoSapBatDau",
                    DoiTuong = "GiaiDau",
                    DoiTuongId = giai.Id,
                    MoTa = $"Cảnh báo tự động: giải '{giai.TenGiai}' bắt đầu ngày mai"
                });
                await context.SaveChangesAsync();

                _logger.LogInformation(
                    "TournamentBG: Cảnh báo giải '{Ten}' bắt đầu ngày mai.", giai.TenGiai);
            }
        }
    }
}