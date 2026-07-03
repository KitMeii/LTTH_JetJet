using System.Net.Http.Json;
using System.Text.Json;

namespace Web_Stadium.Services
{
    public class GoStaffApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public GoStaffApiClient(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;

            var baseUrl = _config["GoStaffApi:BaseUrl"] ?? "http://localhost:8081";
            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            _httpClient.Timeout = TimeSpan.FromSeconds(3);
        }

        public async Task<GoCheckInListResult> GetCheckInsAsync(int matchId, int staffId)
        {
            var endpoint = $"api/tournament/matches/{matchId}/checkins";
            try
            {
                using var request = CreateRequest(HttpMethod.Get, endpoint, staffId);
                using var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return GoCheckInListResult.Disconnected(endpoint, ExtractMessage(body, response.ReasonPhrase));

                var result = JsonSerializer.Deserialize<GoCheckInListResult>(body, _jsonOptions)
                    ?? new GoCheckInListResult();
                result.IsConnected = true;
                result.Endpoint = endpoint;
                result.Message = "Go API connected";
                result.LastSyncAt = DateTime.Now;
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return GoCheckInListResult.Disconnected(endpoint, "Go API chua chay tren localhost:8081");
            }
        }

        public async Task<GoToggleResult> TogglePlayerCheckInAsync(int matchId, int playerId, int staffId)
        {
            var endpoint = $"api/tournament/matches/{matchId}/checkins/{playerId}/toggle";
            try
            {
                using var request = CreateRequest(HttpMethod.Post, endpoint, staffId);
                using var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return GoToggleResult.Failed(endpoint, ExtractMessage(body, response.ReasonPhrase));

                var result = JsonSerializer.Deserialize<GoToggleResult>(body, _jsonOptions)
                    ?? GoToggleResult.Failed(endpoint, "Go API returned empty data");
                result.Endpoint = endpoint;
                result.LastSyncAt = DateTime.Now;
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return GoToggleResult.Failed(endpoint, "Go API chua chay tren localhost:8081");
            }
        }

        public async Task<GoBookingCheckInResult> CheckInBookingAsync(int bookingId, int staffId)
        {
            var endpoint = $"api/staff/bookings/{bookingId}/check-in";
            try
            {
                using var request = CreateRequest(HttpMethod.Post, endpoint, staffId);
                using var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return GoBookingCheckInResult.Failed(endpoint, ExtractMessage(body, response.ReasonPhrase));

                var result = JsonSerializer.Deserialize<GoBookingCheckInResult>(body, _jsonOptions)
                    ?? GoBookingCheckInResult.Failed(endpoint, "Go API returned empty data");
                result.Endpoint = endpoint;
                result.LastSyncAt = DateTime.Now;
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return GoBookingCheckInResult.Failed(endpoint, "Go API chua chay tren localhost:8081");
            }
        }

        public async Task<GoDemoStatusResult> GetDemoStatusAsync()
        {
            const string endpoint = "api/demo/status";
            try
            {
                var result = await _httpClient.GetFromJsonAsync<GoDemoStatusResult>(endpoint, _jsonOptions)
                    ?? new GoDemoStatusResult();
                result.Ok = true;
                result.Endpoint = endpoint;
                result.Message = "Go API connected";
                result.LastSyncAt = DateTime.Now;
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return new GoDemoStatusResult
                {
                    Ok = false,
                    Endpoint = endpoint,
                    Message = "Go API chua chay tren localhost:8081",
                    LastSyncAt = DateTime.Now
                };
            }
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, int staffId)
        {
            var request = new HttpRequestMessage(method, endpoint);
            request.Headers.Add("X-Internal-Api-Key", _config["GoStaffApi:ApiKey"] ?? "dev-demo-key");
            request.Headers.Add("X-Staff-Id", staffId.ToString());
            return request;
        }

        private static string ExtractMessage(string body, string? fallback)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var message))
                    return message.GetString() ?? fallback ?? "Go API error";
            }
            catch (JsonException)
            {
            }

            return fallback ?? "Go API error";
        }
    }

    public class GoCheckInListResult
    {
        public bool IsConnected { get; set; }
        public string Endpoint { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime LastSyncAt { get; set; }
        public int MatchId { get; set; }
        public List<int> CheckedPlayerIds { get; set; } = new();
        public int TotalChecked { get; set; }
        public DateTime? SyncedAt { get; set; }

        public static GoCheckInListResult Disconnected(string endpoint, string message) => new()
        {
            IsConnected = false,
            Endpoint = endpoint,
            Message = message,
            LastSyncAt = DateTime.Now,
            CheckedPlayerIds = new List<int>()
        };
    }

    public class GoToggleResult
    {
        public bool Ok { get; set; }
        public string Endpoint { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime LastSyncAt { get; set; }
        public int MatchId { get; set; }
        public int PlayerId { get; set; }
        public bool Checked { get; set; }
        public int TotalChecked { get; set; }
        public DateTime? SyncedAt { get; set; }

        public static GoToggleResult Failed(string endpoint, string message) => new()
        {
            Ok = false,
            Endpoint = endpoint,
            Message = message,
            LastSyncAt = DateTime.Now
        };
    }

    public class GoBookingCheckInResult
    {
        public bool Ok { get; set; }
        public string Endpoint { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime LastSyncAt { get; set; }
        public int BookingId { get; set; }
        public int StaffId { get; set; }
        public int StadiumId { get; set; }
        public string Confirmation { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string StadiumName { get; set; } = "";
        public bool CheckedIn { get; set; }
        public DateTime? SyncedAt { get; set; }

        public static GoBookingCheckInResult Failed(string endpoint, string message) => new()
        {
            Ok = false,
            Endpoint = endpoint,
            Message = message,
            LastSyncAt = DateTime.Now
        };
    }

    public class GoDemoStatusResult
    {
        public bool Ok { get; set; }
        public string Endpoint { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime LastSyncAt { get; set; }
        public string ServiceName { get; set; } = "";
        public long UptimeSeconds { get; set; }
        public string DbStatus { get; set; } = "";
        public int TotalCheckInRecords { get; set; }
        public int CheckedRecords { get; set; }
        public DateTime? GeneratedAt { get; set; }
    }
}
