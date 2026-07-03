namespace Web_Stadium.Services.Dto.Recommendation
{
    /// <summary>Khop com.pitchhub.tournament.dto.ScheduleResponse (java-recommendation).</summary>
    public class ScheduleResultDto
    {
        public List<AssignmentInfoDto> Assignments { get; set; } = new();
        public List<int> Unassigned { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>Khop com.pitchhub.tournament.dto.AssignmentDto.</summary>
    public class AssignmentInfoDto
    {
        public int MatchId { get; set; }
        public int KhungGioId { get; set; }
        public string? Ngay { get; set; }
    }
}
