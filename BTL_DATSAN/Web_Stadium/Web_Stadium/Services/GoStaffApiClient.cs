using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Web_Stadium.Services.Dto.Go;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Lop trung gian goi sang GoStaffApi (port 8082) — cung pattern voi
    /// TournamentApiService.cs / RecommendationApiService.cs.
    ///
    /// KHAC BIET QUAN TRONG voi 2 client kia: GoStaffApi tra JSON THANH CONG va
    /// JSON LOI co HINH DANG KHAC NHAU (thanh cong khong co field "ok"; loi la
    /// {"ok":false,"message":"..."}) — nen KHONG the doc truc tiep vao 1 kieu T
    /// nhu java-recommendation. Phai kiem tra IsSuccessStatusCode truoc, roi
    /// moi chon kieu de deserialize.
    ///
    /// GetCheckIns() tra ve CheckInListDto tho (chi ID cau thu da diem danh) —
    /// KHONG co ten, vi Go khong luu ten. Muon co danh sach kem ten (giong yeu
    /// cau PlayerCheckInDto) phai ghep voi roster that lay tu
    /// TournamentApiService.GetChiTietTran() o tang Controller — xem
    /// TournamentStaffController.DiemDanhCauThu().
    /// </summary>
    public class GoStaffApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoStaffApiClient> _logger;

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public GoStaffApiClient(IHttpClientFactory httpClientFactory, ILogger<GoStaffApiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string? GetJwtFromContext(HttpContext context) => context.Request.Cookies["jwt"];

        // GET /api/tournament/matches/{matchId}/checkins
        public async Task<(bool ok, string? message, CheckInListDto? data)> GetCheckIns(int matchId, string jwtToken)
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.GetAsync($"api/tournament/matches/{matchId}/checkins");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<CheckInListDto>(ReadOptions);
                    return (true, null, data);
                }

                var error = await response.Content.ReadFromJsonAsync<GoErrorDto>(ReadOptions);
                return (false, error?.Message ?? $"GoStaffApi tra loi {(int)response.StatusCode}", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc GoStaffApi (GET checkins matchId={MatchId})", matchId);
                return (false, "Khong ket noi duoc dich vu diem danh (GoStaffApi)!", null);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi GoStaffApi (GET checkins matchId={MatchId})", matchId);
                return (false, "Dich vu diem danh phan hoi qua cham!", null);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON khong hop le tu GoStaffApi (GET checkins matchId={MatchId})", matchId);
                return (false, "Du lieu tra ve khong hop le!", null);
            }
        }

        // POST /api/tournament/matches/{matchId}/checkins/{playerId}/toggle
        public async Task<(bool ok, string? message, ToggleResultDto? data)> TogglePlayerCheckIn(int matchId, int playerId, string jwtToken)
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.PostAsync($"api/tournament/matches/{matchId}/checkins/{playerId}/toggle", null);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<ToggleResultDto>(ReadOptions);
                    return (true, data?.Message, data);
                }

                var error = await response.Content.ReadFromJsonAsync<GoErrorDto>(ReadOptions);
                return (false, error?.Message ?? $"GoStaffApi tra loi {(int)response.StatusCode}", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc GoStaffApi (POST toggle matchId={MatchId} playerId={PlayerId})", matchId, playerId);
                return (false, "Khong ket noi duoc dich vu diem danh (GoStaffApi)!", null);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi GoStaffApi (POST toggle matchId={MatchId} playerId={PlayerId})", matchId, playerId);
                return (false, "Dich vu diem danh phan hoi qua cham!", null);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON khong hop le tu GoStaffApi (POST toggle matchId={MatchId} playerId={PlayerId})", matchId, playerId);
                return (false, "Du lieu tra ve khong hop le!", null);
            }
        }

        private HttpClient CreateClient(string? jwtToken)
        {
            var client = _httpClientFactory.CreateClient("GoStaffApi");
            if (!string.IsNullOrEmpty(jwtToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            return client;
        }

        private class GoErrorDto
        {
            public bool Ok { get; set; }
            public string? Message { get; set; }
        }
    }
}
