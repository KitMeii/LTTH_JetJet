using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Web_Stadium.Services.Dto.Recommendation;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Lop trung gian goi sang java-recommendation (port 8081) — cung pattern voi
    /// TournamentApiService.cs (goi sang tournament-service port 8080).
    ///
    /// KHAC BIET QUAN TRONG voi TournamentApiService: java-recommendation KHONG boc
    /// JSON trong envelope {success,message,data,timestamp} — moi endpoint tra thang
    /// DTO cua no (VD: CheckInResult{success,message,booking}, hoac Map thuan cho
    /// suggest-price). Vi vay o day doc JSON truc tiep vao kieu dich, khong co
    /// buoc "unwrap Data" nhu ben TournamentApiService.
    ///
    /// Nhiem vu:
    ///  - Dinh kem JWT (tu Request.Cookies["jwt"]) vao header Authorization.
    ///  - Goi API Java, doc JSON truc tiep (khong qua envelope).
    ///  - Neu Java app khong chay / timeout → bat exception, log, tra ve
    ///    null/rong thay vi de loi 500 lam sap trang.
    /// </summary>
    public class RecommendationApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RecommendationApiService> _logger;

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public RecommendationApiService(IHttpClientFactory httpClientFactory, ILogger<RecommendationApiService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>Lay JWT tu cookie — goi tu Controller truoc khi goi cac method ben duoi.</summary>
        public string? GetJwtFromContext(HttpContext context) => context.Request.Cookies["jwt"];

        // ════════════════════════════════════════════════════════════
        // 1. CHECK-IN QR
        // ════════════════════════════════════════════════════════════

        // GET /api/owner/checkin/lookup/{maXacNhan}
        // Tra ve (ok, message, booking) thay vi chi BookingInfoDto — de Controller
        // hien duoc thong bao loi that tu Java (VD: "Khong tim thay don...").
        public async Task<(bool ok, string? message, BookingInfoDto? booking)> LookupDatSan(string maXacNhan, int ownerId, string jwtToken)
        {
            var query = $"?ownerId={ownerId}";
            var result = await GetAsync<CheckInResultDto>(
                $"api/owner/checkin/lookup/{Uri.EscapeDataString(maXacNhan)}{query}", jwtToken);
            if (result == null) return (false, "Khong ket noi duoc dich vu check-in!", null);
            return (result.Success, result.Message, result.Booking);
        }

        // POST /api/owner/checkin/scan — Body Java that: {maXacNhan, ownerId} (KHONG
        // phai staffId — CheckInService.java kiem tra ownerId trung voi SanBongs.OwnerId).
        public async Task<(bool ok, string? message, BookingInfoDto? booking)> ScanCheckIn(string maXacNhan, int ownerId, string jwtToken)
        {
            var body = new { maXacNhan, ownerId };
            var result = await PostAsync<CheckInResultDto>("api/owner/checkin/scan", body, jwtToken);
            if (result == null) return (false, "Khong ket noi duoc dich vu check-in!", null);
            return (result.Success, result.Message, result.Booking);
        }

        // GET /api/owner/checkin/qr/{maXacNhan} — tra ve PNG bytes
        public async Task<byte[]?> GetQRCode(string maXacNhan, string jwtToken)
            => await GetBytesAsync($"api/owner/checkin/qr/{Uri.EscapeDataString(maXacNhan)}", jwtToken);

        // ════════════════════════════════════════════════════════════
        // 2. DE XUAT GIA
        // ════════════════════════════════════════════════════════════

        // POST /api/owner/suggest-price — Body Java that: {sanId, month, year}
        // (KHONG phai {khungGioId} — SuggestionController.java doc Map voi 3 key nay).
        public async Task<SuggestPriceResultDto?> SuggestPrice(int sanId, int month, int year, string jwtToken)
        {
            var body = new { sanId, month, year };
            return await PostAsync<SuggestPriceResultDto>("api/owner/suggest-price", body, jwtToken);
        }

        // ════════════════════════════════════════════════════════════
        // 3. XUAT PDF BAO CAO
        // ════════════════════════════════════════════════════════════

        // POST /api/report/pdf — tra ve PDF bytes
        public async Task<byte[]?> ExportPDF(ReportDataDto data, string jwtToken)
            => await PostBytesAsync("api/report/pdf", data, jwtToken);

        // ════════════════════════════════════════════════════════════
        // 4. XEP LICH CSP (giai dau)
        // ════════════════════════════════════════════════════════════

        // POST /api/tournament/schedule
        public async Task<ScheduleResultDto?> XepLich(ScheduleRequestDto request, string jwtToken)
            => await PostAsync<ScheduleResultDto>("api/tournament/schedule", request, jwtToken);

        // POST /api/tournament/validate
        public async Task<ValidateResultDto?> ValidateLich(ValidateRequestDto request, string jwtToken)
            => await PostAsync<ValidateResultDto>("api/tournament/validate", request, jwtToken);

        // ════════════════════════════════════════════════════════════
        // HTTP helpers — dung chung, tu bat loi ket noi/timeout.
        // KHAC TournamentApiService: doc thang JSON vao T, KHONG unwrap qua envelope
        // {success,data,message} vi java-recommendation khong dung pattern do.
        // ════════════════════════════════════════════════════════════

        private HttpClient CreateClient(string? jwtToken)
        {
            var client = _httpClientFactory.CreateClient("RecommendationService");
            if (!string.IsNullOrEmpty(jwtToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            return client;
        }

        private async Task<T?> GetAsync<T>(string path, string? jwtToken) where T : class
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.GetAsync(path);
                // Java tra JSON body ke ca khi loi nghiep vu (200 voi success=false, hoac 400) —
                // van co gang doc, khong dua vao IsSuccessStatusCode.
                return await response.Content.ReadFromJsonAsync<T>(ReadOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc java-recommendation (GET {Path})", path);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi java-recommendation (GET {Path})", path);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON khong hop le tu java-recommendation (GET {Path})", path);
                return null;
            }
        }

        private async Task<T?> PostAsync<T>(string path, object body, string? jwtToken) where T : class
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.PostAsJsonAsync(path, body, WriteOptions);
                return await response.Content.ReadFromJsonAsync<T>(ReadOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc java-recommendation (POST {Path})", path);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi java-recommendation (POST {Path})", path);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON khong hop le tu java-recommendation (POST {Path})", path);
                return null;
            }
        }

        private async Task<byte[]?> GetBytesAsync(string path, string? jwtToken)
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.GetAsync(path);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc java-recommendation (GET bytes {Path})", path);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi java-recommendation (GET bytes {Path})", path);
                return null;
            }
        }

        private async Task<byte[]?> PostBytesAsync(string path, object body, string? jwtToken)
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.PostAsJsonAsync(path, body, WriteOptions);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc java-recommendation (POST bytes {Path})", path);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi java-recommendation (POST bytes {Path})", path);
                return null;
            }
        }
    }
}
