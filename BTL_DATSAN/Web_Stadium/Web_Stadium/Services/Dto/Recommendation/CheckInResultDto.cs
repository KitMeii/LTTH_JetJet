namespace Web_Stadium.Services.Dto.Recommendation
{
    /// <summary>
    /// Khop dung field cua com.pitchhub.checkin.dto.CheckInResult (java-recommendation).
    /// LUU Y: khac mo ta ban dau — Java tra ve ca "booking" kem theo (khong chi Success/Message),
    /// nen o day giu du 3 field de LookupDatSan()/ScanCheckIn() lay duoc du lieu don dat san.
    /// </summary>
    public class CheckInResultDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public BookingInfoDto? Booking { get; set; }
    }
}
