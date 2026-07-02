namespace Web_Stadium.Services
{
    /// <summary>
    /// Payload từ view XemTruocLich khi owner bấm "Chốt lịch".
    /// matchId là virtual index (0..N-1) khớp thứ tự trận Berger.
    /// </summary>
    public class ChotLichAssignmentDto
    {
        public int MatchId { get; set; }
        public int KhungGioId { get; set; }
        /// <summary>yyyy-MM-dd</summary>
        public string Ngay { get; set; } = "";
    }
}
