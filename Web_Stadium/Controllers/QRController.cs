using Microsoft.AspNetCore.Mvc;

namespace Web_Stadium.Controllers
{
    public class QRController : Controller
    {
        private readonly IConfiguration _config;

        public QRController(IConfiguration config)
        {
            _config = config;
        }

        // GET /QR/VietQR?amount=100000&info=ABC123
        // Trả về ảnh QR từ VietQR.io (dịch vụ miễn phí)
        [HttpGet]
        public async Task<IActionResult> VietQR(decimal amount, string? info = "")
        {
            var bankId  = _config["Payment:BankId"] ?? "970407"; // Techcombank
            var accNo   = _config["Payment:AccountNumber"] ?? "19038888888888";
            var accName = Uri.EscapeDataString(_config["Payment:AccountName"] ?? "PITCHHUB");
            var addInfo = Uri.EscapeDataString(info ?? "");

            var url = $"https://img.vietqr.io/image/{bankId}-{accNo}-compact2.png" +
                      $"?amount={amount}&addInfo={addInfo}&accountName={accName}";

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                var bytes = await http.GetByteArrayAsync(url);
                return File(bytes, "image/png");
            }
            catch
            {
                // Trả về placeholder nếu không lấy được QR
                return Redirect($"https://placehold.co/240x240/1a2744/16C47F?text=QR+{amount:N0}");
            }
        }
    }
}
