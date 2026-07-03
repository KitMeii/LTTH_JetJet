namespace Web_Stadium.Services.Dto.Recommendation
{
    /// <summary>Khop com.pitchhub.tournament.dto.ScheduleRequest (java-recommendation).</summary>
    public class ScheduleRequestDto
    {
        public int GiaiId { get; set; }
        public List<MatchInfoDto> Matches { get; set; } = new();
        public List<SlotInfoDto> AvailableSlots { get; set; } = new();
        public List<BookingConflictInfoDto> Bookings { get; set; } = new();
        public ConstraintsInfoDto Constraints { get; set; } = new();
    }

    /// <summary>Khop com.pitchhub.tournament.dto.MatchDto.</summary>
    public class MatchInfoDto
    {
        public int MatchId { get; set; }
        public int TeamA { get; set; }
        public int TeamB { get; set; }
        public string? TeamAName { get; set; }
        public string? TeamBName { get; set; }
        public int Round { get; set; }
        public int GroupId { get; set; }
        public string? GroupName { get; set; }
    }

    /// <summary>
    /// Khop com.pitchhub.tournament.dto.SlotDto — Ngay la string "yyyy-MM-dd"
    /// (Java dung LocalDate.parse(...), phai gui dung dinh dang chuoi nay).
    /// </summary>
    public class SlotInfoDto
    {
        public int KhungGioId { get; set; }
        public string Ngay { get; set; } = "";
        public string? GioBatDau { get; set; }
        public string? GioKetThuc { get; set; }
    }

    /// <summary>Khop com.pitchhub.tournament.dto.BookingConflictDto.</summary>
    public class BookingConflictInfoDto
    {
        public int KhungGioId { get; set; }
        public string Ngay { get; set; } = "";
    }

    /// <summary>Khop com.pitchhub.tournament.dto.ConstraintsDto.</summary>
    public class ConstraintsInfoDto
    {
        public int MinRestDays { get; set; } = 1;
        public bool ForbidSameDay { get; set; } = true;
        public bool PreferWeekend { get; set; } = true;
    }
}
