namespace Web_Stadium.Services.Dto.Go
{
    /// <summary>
    /// Hang hien thi cho UI diem danh — do TournamentStaffController tu ghep
    /// (KHONG phai deserialize thang tu Go, vi Go chi tra ID). Nguon:
    ///   - Ten/SoAo/Doi: TournamentApiService.GetChiTietTran() (roster that,
    ///     tournament-service Java — KHONG sua tournament-service, chi goi).
    ///   - IsCheckedIn: co trong CheckInListDto.CheckedPlayerIds (Go) khong.
    /// </summary>
    public class PlayerCheckInDto
    {
        public int ThanhVienDoiId { get; set; }
        public string TenCauThu { get; set; } = "";
        public int SoAo { get; set; }
        public string Doi { get; set; } = ""; // "nha" | "khach"
        public bool IsCheckedIn { get; set; }
        public DateTime? CheckedAt { get; set; }
    }
}
