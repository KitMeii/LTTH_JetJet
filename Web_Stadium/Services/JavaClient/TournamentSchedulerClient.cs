using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Web_Stadium.Services.JavaClient
{
    // ══════════════════════════════════════════════════════════════════
    // DTO — song ánh với Java (com.pitchhub.tournament.dto.*)
    // Đặt cùng file cho gọn (nhóm nhỏ, không tái sử dụng nơi khác).
    // ══════════════════════════════════════════════════════════════════

    public class MatchDto
    {
        public int MatchId { get; set; }
        public int TeamA { get; set; }
        public int TeamB { get; set; }
        public string TeamAName { get; set; } = "";
        public string TeamBName { get; set; } = "";
        public int Round { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; } = "";
    }

    public class SlotDto
    {
        public int KhungGioId { get; set; }
        /// <summary>yyyy-MM-dd</summary>
        public string Ngay { get; set; } = "";
        public string GioBatDau { get; set; } = "";
        public string GioKetThuc { get; set; } = "";
    }

    public class BookingConflictDto
    {
        public int KhungGioId { get; set; }
        public string Ngay { get; set; } = "";
    }

    public class ConstraintsDto
    {
        public int MinRestDays { get; set; } = 1;
        public bool ForbidSameDay { get; set; } = true;
        public bool PreferWeekend { get; set; } = true;
    }

    public class AssignmentDto
    {
        public int MatchId { get; set; }
        public int KhungGioId { get; set; }
        /// <summary>yyyy-MM-dd</summary>
        public string Ngay { get; set; } = "";
    }

    public class ScheduleRequest
    {
        public int GiaiId { get; set; }
        public List<MatchDto> Matches { get; set; } = new();
        public List<SlotDto> AvailableSlots { get; set; } = new();
        public List<BookingConflictDto> Bookings { get; set; } = new();
        public ConstraintsDto Constraints { get; set; } = new();
    }

    public class ScheduleResponse
    {
        public List<AssignmentDto> Assignments { get; set; } = new();
        public List<int> Unassigned { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ValidateRequest
    {
        public int MatchId { get; set; }
        public int TeamA { get; set; }
        public int TeamB { get; set; }
        public int KhungGioId { get; set; }
        public string Ngay { get; set; } = "";
        public List<AssignmentDto> ExistingAssignments { get; set; } = new();
        public List<BookingConflictDto> Bookings { get; set; } = new();
        public List<MatchDto> AllMatches { get; set; } = new();
        public ConstraintsDto Constraints { get; set; } = new();
    }

    public class ValidateResponse
    {
        public bool Ok { get; set; }
        public string? Reason { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════
    // Client — typed HttpClient, đăng ký trong Program.cs
    // ══════════════════════════════════════════════════════════════════

    public class TournamentSchedulerClient
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public TournamentSchedulerClient(HttpClient http)
        {
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<ScheduleResponse> SolveAsync(ScheduleRequest req, CancellationToken ct = default)
        {
            var res = await _http.PostAsJsonAsync("api/tournament/schedule", req, _jsonOpts, ct);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<ScheduleResponse>(_jsonOpts, ct);
            return body ?? new ScheduleResponse();
        }

        public async Task<ValidateResponse> ValidateAsync(ValidateRequest req, CancellationToken ct = default)
        {
            var res = await _http.PostAsJsonAsync("api/tournament/validate", req, _jsonOpts, ct);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<ValidateResponse>(_jsonOpts, ct);
            return body ?? new ValidateResponse { Ok = false, Reason = "Java trả về rỗng." };
        }
    }
}
