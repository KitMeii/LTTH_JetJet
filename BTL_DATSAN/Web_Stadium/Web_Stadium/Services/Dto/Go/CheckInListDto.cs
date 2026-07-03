namespace Web_Stadium.Services.Dto.Go
{
    /// <summary>
    /// Khop dung JSON that tra ve tu GoStaffApi GET /api/tournament/matches/{id}/checkins
    /// (models.CheckInList trong Go) — CHI la danh sach ID cau thu da diem danh,
    /// KHONG kem ten (Go khong luu ten cau thu). Muon co ten phai JOIN voi roster
    /// lay tu TournamentApiService.GetChiTietTran() — xem PlayerCheckInDto.
    /// </summary>
    public class CheckInListDto
    {
        public int MatchId { get; set; }
        public List<int> CheckedPlayerIds { get; set; } = new();
        public int TotalChecked { get; set; }
        public DateTime SyncedAt { get; set; }
    }
}
