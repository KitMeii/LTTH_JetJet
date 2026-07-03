namespace Web_Stadium.Services.Dto.Recommendation
{
    /// <summary>Khop dung field cua com.pitchhub.report.dto.ReportData (java-recommendation).</summary>
    public class ReportDataDto
    {
        public string? Title { get; set; }
        public string? StadiumName { get; set; }
        public string? Period { get; set; }
        public ReportSummaryDto? Summary { get; set; }
        public List<ReportDetailDto> Details { get; set; } = new();
    }

    /// <summary>Khop com.pitchhub.report.dto.ReportSummary.</summary>
    public class ReportSummaryDto
    {
        public double TotalRevenue { get; set; }
        public int TotalBookings { get; set; }
        public double AvgPerBooking { get; set; }
        public double Commission { get; set; }
        public double NetRevenue { get; set; }
    }

    /// <summary>Khop com.pitchhub.report.dto.ReportDetail.</summary>
    public class ReportDetailDto
    {
        public string? Label { get; set; }
        public double TotalRevenue { get; set; }
        public int Bookings { get; set; }
        public double NetRevenue { get; set; }
    }
}
