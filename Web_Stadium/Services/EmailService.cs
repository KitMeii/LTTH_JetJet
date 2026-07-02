using System.Net;
using System.Net.Mail;

namespace Web_Stadium.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ── Gửi email cơ bản ─────────────────────────────────────
        public async Task GuiEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            try
            {
                var host = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["Email:SmtpPort"] ?? "587");
                var sender = _config["Email:SenderEmail"]!;
                var senderName = _config["Email:SenderName"] ?? "PitchHub";
                var password = _config["Email:AppPassword"]!;

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(sender, password)
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(sender, senderName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mail.To.Add(new MailAddress(toEmail, toName));

                await client.SendMailAsync(mail);
                _logger.LogInformation("✅ Email gửi OK → {Email} | {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Lỗi gửi email → {Email}: {Msg}", toEmail, ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        // 1. EMAIL XÁC NHẬN ĐẶT SÂN (Owner đã duyệt)
        // ══════════════════════════════════════════════════════════
        public async Task GuiEmailXacNhanDatSan(
            string toEmail, string toName,
            string tenSan, string diaChi,
            string khungGio, string ngayThiDau,
            string maXacNhan, decimal tienCoc)
        {
            var mapsUrl = $"https://maps.google.com/?q={Uri.EscapeDataString(diaChi)}";
            var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={maXacNhan}";

            var body = $@"
<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='utf-8'></head>
<body style='font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:20px;'>
<div style='max-width:560px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;
            box-shadow:0 4px 20px rgba(0,0,0,.1);'>

  <!-- Header -->
  <div style='background:linear-gradient(135deg,#0f2027,#1a3a2a);padding:32px 28px;text-align:center;'>
    <div style='font-size:2rem;font-weight:900;color:#fff;letter-spacing:-1px;'>
      PITCH<span style='color:#1ed760;'>HUB</span>⚽
    </div>
    <div style='color:#1ed760;font-size:.85rem;letter-spacing:2px;margin-top:4px;'>
      ĐẶT SÂN THÀNH CÔNG
    </div>
  </div>

  <!-- Body -->
  <div style='padding:28px;'>
    <p style='color:#333;font-size:1rem;margin-bottom:20px;'>
      Xin chào <strong>{toName}</strong>,<br>
      Đơn đặt sân của bạn đã được xác nhận. Hẹn gặp bạn trên sân! ⚽
    </p>

    <!-- Info box -->
    <div style='background:#f8fffe;border:1px solid #1ed760;border-radius:12px;padding:20px;margin-bottom:20px;'>
      <table style='width:100%;border-collapse:collapse;font-size:.9rem;'>
        <tr><td style='color:#888;padding:6px 0;width:130px;'>🏟 Sân</td>
            <td style='color:#111;font-weight:700;'>{tenSan}</td></tr>
        <tr><td style='color:#888;padding:6px 0;'>📍 Địa chỉ</td>
            <td style='color:#111;'>{diaChi}</td></tr>
        <tr><td style='color:#888;padding:6px 0;'>📅 Ngày</td>
            <td style='color:#111;font-weight:600;'>{ngayThiDau}</td></tr>
        <tr><td style='color:#888;padding:6px 0;'>⏰ Giờ</td>
            <td style='color:#111;font-weight:600;'>{khungGio}</td></tr>
        <tr><td style='color:#888;padding:6px 0;'>💳 Tiền cọc</td>
            <td style='color:#1ed760;font-weight:700;'>{tienCoc:N0}đ</td></tr>
      </table>
    </div>

    <!-- Mã xác nhận -->
    <div style='background:#0f1f14;border-radius:12px;padding:18px;text-align:center;margin-bottom:20px;'>
      <div style='color:#888;font-size:.78rem;letter-spacing:1.5px;margin-bottom:8px;'>MÃ XÁC NHẬN CHECK-IN</div>
      <div style='font-family:monospace;font-size:1.6rem;font-weight:900;color:#1ed760;letter-spacing:3px;'>
        {maXacNhan}
      </div>
      <div style='color:#555;font-size:.75rem;margin-top:6px;'>Đưa mã này cho Staff khi đến sân</div>
    </div>

    <!-- QR Code -->
    <div style='text-align:center;margin-bottom:20px;'>
      <div style='color:#888;font-size:.78rem;letter-spacing:1px;margin-bottom:10px;'>HOẶC QUÉT QR CODE</div>
      <img src='{qrUrl}' width='160' height='160'
           style='border-radius:12px;border:3px solid #1ed760;' alt='QR Code' />
    </div>

    <!-- Google Maps -->
    <div style='text-align:center;margin-bottom:24px;'>
      <a href='{mapsUrl}' target='_blank'
         style='display:inline-block;background:#1ed760;color:#000;font-weight:700;
                padding:12px 28px;border-radius:10px;text-decoration:none;font-size:.9rem;'>
        📍 Chỉ đường đến sân
      </a>
    </div>

    <p style='color:#aaa;font-size:.8rem;text-align:center;'>
      Bạn sẽ nhận email nhắc lịch trước 24 giờ và 1 giờ trước trận.
    </p>
  </div>

  <!-- Footer -->
  <div style='background:#f8f8f8;padding:16px;text-align:center;border-top:1px solid #eee;'>
    <p style='color:#aaa;font-size:.75rem;margin:0;'>
      © {DateTime.Now.Year} PitchHub.vn &nbsp;·&nbsp; support@pitchhub.vn
    </p>
  </div>
</div>
</body></html>";

            await GuiEmailAsync(toEmail, toName,
                $"✅ Xác nhận đặt sân — {tenSan} | {ngayThiDau}", body);
        }

        // ══════════════════════════════════════════════════════════
        // 2. EMAIL NHẮC LỊCH (24h và 1h trước trận)
        // ══════════════════════════════════════════════════════════
        public async Task GuiEmailNhacLich(
            string toEmail, string toName,
            string tenSan, string diaChi,
            string khungGio, string ngayThiDau,
            string maXacNhan, bool la24h)
        {
            var mapsUrl = $"https://maps.google.com/?q={Uri.EscapeDataString(diaChi)}";
            var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=180x180&data={maXacNhan}";
            var tieuDe = la24h ? "⏰ Nhắc lịch: Còn 24 giờ nữa là đến trận!"
                                : "🔔 Nhắc lịch: Còn 1 giờ nữa — Chuẩn bị ra sân!";
            var subtitle = la24h ? "Trận đấu của bạn diễn ra vào ngày mai!"
                                 : "Trận đấu của bạn sắp bắt đầu!";

            var body = $@"
<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='utf-8'></head>
<body style='font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:20px;'>
<div style='max-width:560px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;
            box-shadow:0 4px 20px rgba(0,0,0,.1);'>

  <div style='background:linear-gradient(135deg,#0f2027,#1a3a2a);padding:28px;text-align:center;'>
    <div style='font-size:1.8rem;font-weight:900;color:#fff;'>PITCH<span style='color:#1ed760;'>HUB</span>⚽</div>
    <div style='color:#ffc107;font-size:.85rem;letter-spacing:2px;margin-top:4px;'>{(la24h ? "NHẮC LỊCH — 24 GIỜ" : "NHẮC LỊCH — 1 GIỜ")}</div>
  </div>

  <div style='padding:28px;'>
    <p style='color:#333;font-size:1rem;margin-bottom:20px;'>
      Xin chào <strong>{toName}</strong>,<br>
      {subtitle} Đừng quên chuẩn bị đầy đủ nhé! 💪
    </p>

    <div style='background:#f8fffe;border:1px solid #1ed760;border-radius:12px;padding:20px;margin-bottom:20px;'>
      <table style='width:100%;border-collapse:collapse;font-size:.9rem;'>
        <tr><td style='color:#888;padding:6px 0;width:130px;'>🏟 Sân</td>
            <td style='color:#111;font-weight:700;'>{tenSan}</td></tr>
        <tr><td style='color:#888;padding:6px 0;'>📍 Địa chỉ</td>
            <td style='color:#111;'>{diaChi}</td></tr>
        <tr><td style='color:#888;padding:6px 0;'>📅 Ngày</td>
            <td style='color:#111;font-weight:600;'>{ngayThiDau}</td></tr>
        <tr><td style='color:#888;padding:6px 0;'>⏰ Giờ</td>
            <td style='color:#111;font-weight:700;color:#e74c3c;'>{khungGio}</td></tr>
      </table>
    </div>

    <div style='background:#0f1f14;border-radius:12px;padding:16px;text-align:center;margin-bottom:20px;'>
      <div style='color:#888;font-size:.75rem;letter-spacing:1.5px;margin-bottom:6px;'>MÃ CHECK-IN</div>
      <div style='font-family:monospace;font-size:1.4rem;font-weight:900;color:#1ed760;letter-spacing:3px;'>{maXacNhan}</div>
    </div>

    <div style='text-align:center;margin-bottom:20px;'>
      <img src='{qrUrl}' width='140' height='140'
           style='border-radius:10px;border:3px solid #1ed760;' alt='QR' />
    </div>

    <div style='text-align:center;'>
      <a href='{mapsUrl}' target='_blank'
         style='display:inline-block;background:#1ed760;color:#000;font-weight:700;
                padding:12px 28px;border-radius:10px;text-decoration:none;font-size:.9rem;'>
        📍 Chỉ đường đến sân
      </a>
    </div>
  </div>

  <div style='background:#f8f8f8;padding:16px;text-align:center;border-top:1px solid #eee;'>
    <p style='color:#aaa;font-size:.75rem;margin:0;'>© {DateTime.Now.Year} PitchHub.vn</p>
  </div>
</div>
</body></html>";

            await GuiEmailAsync(toEmail, toName, tieuDe, body);
        }

        // ══════════════════════════════════════════════════════════
        // 3. EMAIL MỜI ĐÁNH GIÁ (30 phút sau HoanThanh)
        // ══════════════════════════════════════════════════════════
        public async Task GuiEmailMoiDanhGia(
            string toEmail, string toName,
            string tenSan, int sanBongId, int datSanId)
        {
            var linkDanhGia = $"https://pitchhub.vn/Venues/Details/{sanBongId}#danh-gia";

            var body = $@"
<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='utf-8'></head>
<body style='font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:20px;'>
<div style='max-width:560px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;
            box-shadow:0 4px 20px rgba(0,0,0,.1);'>

  <div style='background:linear-gradient(135deg,#0f2027,#1a3a2a);padding:28px;text-align:center;'>
    <div style='font-size:1.8rem;font-weight:900;color:#fff;'>PITCH<span style='color:#1ed760;'>HUB</span>⚽</div>
    <div style='color:#ffc107;font-size:.85rem;letter-spacing:2px;margin-top:4px;'>TRẬN ĐẤU ĐÃ KẾT THÚC</div>
  </div>

  <div style='padding:28px;text-align:center;'>
    <div style='font-size:3rem;margin-bottom:12px;'>⭐</div>
    <h2 style='color:#111;font-size:1.2rem;margin-bottom:12px;'>
      Bạn cảm thấy thế nào về <strong>{tenSan}</strong>?
    </h2>
    <p style='color:#666;font-size:.9rem;margin-bottom:24px;line-height:1.6;'>
      Đánh giá của bạn giúp cộng đồng PitchHub chọn được sân tốt hơn.<br>
      Chỉ mất 30 giây và bạn nhận ngay <strong style='color:#ffc107;'>+5 điểm thưởng ⭐</strong>
    </p>

    <a href='{linkDanhGia}' target='_blank'
       style='display:inline-block;background:#1ed760;color:#000;font-weight:700;
              padding:14px 36px;border-radius:12px;text-decoration:none;font-size:1rem;
              box-shadow:0 4px 15px rgba(30,215,96,.3);'>
      ⭐ Đánh giá ngay
    </a>

    <p style='color:#aaa;font-size:.75rem;margin-top:20px;'>
      Đánh giá theo 3 tiêu chí: Chất lượng cỏ · Cơ sở vật chất · Thái độ nhân viên
    </p>
  </div>

  <div style='background:#f8f8f8;padding:16px;text-align:center;border-top:1px solid #eee;'>
    <p style='color:#aaa;font-size:.75rem;margin:0;'>© {DateTime.Now.Year} PitchHub.vn</p>
  </div>
</div>
</body></html>";

            await GuiEmailAsync(toEmail, toName,
                $"⭐ Đánh giá {tenSan} và nhận +5 điểm thưởng!", body);
        }

        // ══════════════════════════════════════════════════════════
        // 4. EMAIL THÔNG BÁO HỦY ĐƠN
        // ══════════════════════════════════════════════════════════
        public async Task GuiEmailHuyDon(
            string toEmail, string toName,
            string tenSan, string ngayThiDau,
            string lyDo, decimal soTienHoan)
        {
            var body = $@"
<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='utf-8'></head>
<body style='font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:20px;'>
<div style='max-width:560px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;
            box-shadow:0 4px 20px rgba(0,0,0,.1);'>

  <div style='background:linear-gradient(135deg,#1a0a0a,#2a1010);padding:28px;text-align:center;'>
    <div style='font-size:1.8rem;font-weight:900;color:#fff;'>PITCH<span style='color:#1ed760;'>HUB</span>⚽</div>
    <div style='color:#e74c3c;font-size:.85rem;letter-spacing:2px;margin-top:4px;'>THÔNG BÁO HỦY ĐƠN</div>
  </div>

  <div style='padding:28px;'>
    <p style='color:#333;'>Xin chào <strong>{toName}</strong>,</p>
    <p style='color:#333;'>Đơn đặt sân <strong>{tenSan}</strong> ngày <strong>{ngayThiDau}</strong> đã bị hủy.</p>
    <p style='color:#666;'>Lý do: {lyDo}</p>

    <div style='background:#fff8f0;border:1px solid #f59e0b;border-radius:12px;padding:18px;margin:20px 0;text-align:center;'>
      <div style='color:#888;font-size:.8rem;margin-bottom:6px;'>SỐ TIỀN HOÀN TRẢ</div>
      <div style='font-size:1.8rem;font-weight:900;color:#f59e0b;'>{soTienHoan:N0}đ</div>
      <div style='color:#aaa;font-size:.75rem;margin-top:6px;'>Hoàn về phương thức thanh toán ban đầu trong 1–3 ngày làm việc</div>
    </div>

    <p style='color:#888;font-size:.85rem;'>
      Nếu có thắc mắc, vui lòng liên hệ <a href='mailto:support@pitchhub.vn'>support@pitchhub.vn</a>
    </p>
  </div>

  <div style='background:#f8f8f8;padding:16px;text-align:center;border-top:1px solid #eee;'>
    <p style='color:#aaa;font-size:.75rem;margin:0;'>© {DateTime.Now.Year} PitchHub.vn</p>
  </div>
</div>
</body></html>";

            await GuiEmailAsync(toEmail, toName,
                $"❌ Đơn đặt sân {tenSan} đã bị hủy", body);
        }
    }
}