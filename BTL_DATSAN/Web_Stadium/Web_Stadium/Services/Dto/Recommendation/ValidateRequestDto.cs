namespace Web_Stadium.Services.Dto.Recommendation
{
    /// <summary>Khop com.pitchhub.tournament.dto.ValidateRequest (java-recommendation).</summary>
    public class ValidateRequestDto
    {
        public int MatchId { get; set; }
        public int TeamA { get; set; }
        public int TeamB { get; set; }
        public int KhungGioId { get; set; }
        public string Ngay { get; set; } = "";
        public List<AssignmentInfoDto> ExistingAssignments { get; set; } = new();
        public List<BookingConflictInfoDto> Bookings { get; set; } = new();
        public List<MatchInfoDto> AllMatches { get; set; } = new();
        public ConstraintsInfoDto Constraints { get; set; } = new();
    }
}
