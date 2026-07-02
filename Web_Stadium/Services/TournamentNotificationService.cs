using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Toàn bộ email/notification cho giải đấu
    /// Tách khỏi nghiệp vụ — fail không ảnh hưởng domain
    /// </summary>
    public class TournamentNotificationService
    {
        private readonly SanBongContext _context;
        private readonly EmailService _emailService;

        public TournamentNotificationService(
            SanBongContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // ══════════════════════════════════════════════════════════
        // Gửi email lịch thi đấu cho đội trưởng khi giải khởi động
        // ══════════════════════════════════════════════════════════
        public async Task GuiEmailLichDau(int giaiDauId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.DoiBongs).ThenInclude(d => d.DoiTruong)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiNha)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiKhach)
                .FirstOrDefaultAsync(g => g.Id == giaiDauId);

            if (giai == null) return;

            var lichHtml = BuildLichHtml(giai.TranDaus.ToList());
            var linkGiai = $"https://pitchhub.vn/TournamentPublic/Details/{giaiDauId}";

            var doiTruongs = giai.DoiBongs
                .Where(d => d.DaThanhToan && d.DoiTruong != null)
                .Select(d => d.DoiTruong!)
                .DistinctBy(u => u.Email)
                .ToList();

            foreach (var dt in doiTruongs)
            {
                var body = $@"
                <div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
                    <div style='background:linear-gradient(135deg,#0f2027,#0EA86A);
                                padding:24px;text-align:center;border-radius:12px 12px 0 0;'>
                        <div style='font-size:1.5rem;font-weight:900;color:#fff;'>
                            PITCH<span style='color:#1ed760;'>HUB</span> ⚽
                        </div>
                    </div>
                    <div style='padding:24px;background:#fff;border-radius:0 0 12px 12px;'>
                        <h2>🏆 {giai.TenGiai} — Lịch thi đấu</h2>
                        <p>Xin chào <strong>{dt.HoTen}</strong>, giải đấu đã bắt đầu!</p>
                        {lichHtml}
                        <div style='margin-top:20px;text-align:center;'>
                            <a href='{linkGiai}' style='background:#0EA86A;color:#fff;
                               padding:12px 24px;border-radius:10px;
                               text-decoration:none;font-weight:700;'>
                                Xem trực tiếp BXH & Lịch đấu →
                            </a>
                        </div>
                    </div>
                </div>";

                try
                {
                    await _emailService.GuiEmailAsync(
                        dt.Email, dt.HoTen,
                        $"🏆 Lịch thi đấu — {giai.TenGiai}", body);
                }
                catch { /* Log lỗi — không rethrow để không rollback domain */ }
            }
        }

        // ══════════════════════════════════════════════════════════
        // Gửi email xác nhận đăng ký đội
        // ══════════════════════════════════════════════════════════
        public async Task GuiEmailXacNhanDangKy(int doiId)
        {
            var doi = await _context.DoiBongs
                .Include(d => d.GiaiDau)
                .Include(d => d.DoiTruong)
                .FirstOrDefaultAsync(d => d.Id == doiId);

            if (doi?.DoiTruong == null) return;

            var linkGiai = $"https://pitchhub.vn/TournamentPublic/Details/{doi.GiaiDauId}";
            var body = $@"
            <div style='font-family:Arial,sans-serif;max-width:560px;margin:0 auto;'>
                <div style='background:linear-gradient(135deg,#0f2027,#0EA86A);
                            padding:24px;text-align:center;border-radius:12px 12px 0 0;'>
                    <div style='font-size:1.5rem;font-weight:900;color:#fff;'>
                        PITCH<span style='color:#1ed760;'>HUB</span> ⚽
                    </div>
                </div>
                <div style='padding:24px;background:#fff;border-radius:0 0 12px 12px;'>
                    <h2>🎉 Đăng ký thành công!</h2>
                    <p>Xin chào <strong>{doi.DoiTruong.HoTen}</strong>,</p>
                    <p>Đội <strong style='color:#0EA86A;'>{doi.TenDoi}</strong>
                       đã đăng ký tham dự giải
                       <strong>{doi.GiaiDau.TenGiai}</strong>.</p>
                    <div style='background:#f0f9f5;border-radius:10px;padding:16px;margin:16px 0;'>
                        <div style='font-size:13px;color:#666;margin-bottom:4px;'>Bước tiếp theo</div>
                        <div style='font-weight:700;color:#0EA86A;'>
                            Nộp danh sách cầu thủ trước khi giải đóng đăng ký!
                        </div>
                    </div>
                    <a href='{linkGiai}' style='display:inline-block;background:#0EA86A;
                       color:#fff;padding:12px 24px;border-radius:10px;
                       text-decoration:none;font-weight:700;'>
                        Xem giải đấu →
                    </a>
                </div>
            </div>";

            try
            {
                await _emailService.GuiEmailAsync(
                    doi.DoiTruong.Email, doi.DoiTruong.HoTen,
                    $"🎉 Đăng ký giải '{doi.GiaiDau.TenGiai}' thành công!", body);
            }
            catch { /* fire and forget */ }
        }

        // ══════════════════════════════════════════════════════════
        // Gửi email kết quả trận cho 2 đội trưởng
        // ══════════════════════════════════════════════════════════
        public async Task GuiEmailKetQuaTran(int tranDauId)
        {
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau)
                .Include(t => t.DoiNha).ThenInclude(d => d.DoiTruong)
                .Include(t => t.DoiKhach).ThenInclude(d => d.DoiTruong)
                .FirstOrDefaultAsync(t => t.Id == tranDauId);

            if (tran == null) return;

            var tyso = $"{tran.BanThangNha} – {tran.BanThangKhach}";
            var tieuDe = $"⚽ Kết quả: {tran.DoiNha?.TenDoi} {tyso} {tran.DoiKhach?.TenDoi}";

            var doiTruongs = new[] { tran.DoiNha?.DoiTruong, tran.DoiKhach?.DoiTruong }
                .Where(dt => dt != null).ToList();

            foreach (var dt in doiTruongs)
            {
                if (dt == null) continue;
                var body = $@"
                <div style='font-family:Arial;padding:20px;'>
                    <h2>⚽ Kết quả trận đấu</h2>
                    <div style='font-size:2rem;font-weight:900;text-align:center;
                                color:#0EA86A;padding:20px;'>{tyso}</div>
                    <p>{tran.DoiNha?.TenDoi} vs {tran.DoiKhach?.TenDoi}</p>
                    <p>Vòng {tran.VongDau} — {tran.GiaiDau.TenGiai}</p>
                </div>";

                try { await _emailService.GuiEmailAsync(dt.Email, dt.HoTen, tieuDe, body); }
                catch { }
            }
        }

        // ── Helper build HTML lịch đấu ─────────────────────────
        private string BuildLichHtml(List<TranDau> tranDaus)
        {
            var html = @"<table style='border-collapse:collapse;width:100%;'>
                <tr style='background:#0EA86A;color:#fff;'>
                    <th style='padding:8px;border:1px solid #ddd;'>Vòng</th>
                    <th style='padding:8px;border:1px solid #ddd;'>Đội nhà</th>
                    <th style='padding:8px;border:1px solid #ddd;'>Đội khách</th>
                    <th style='padding:8px;border:1px solid #ddd;'>Ngày</th>
                </tr>";

            foreach (var t in tranDaus.OrderBy(t => t.VongDau))
            {
                html += $@"<tr>
                    <td style='padding:8px;border:1px solid #ddd;text-align:center;'>
                        Vòng {t.VongDau}</td>
                    <td style='padding:8px;border:1px solid #ddd;'>{t.DoiNha?.TenDoi}</td>
                    <td style='padding:8px;border:1px solid #ddd;'>{t.DoiKhach?.TenDoi}</td>
                    <td style='padding:8px;border:1px solid #ddd;'>
                        {t.NgayThiDau:dd/MM/yyyy}</td>
                </tr>";
            }

            return html + "</table>";
        }
    }
}