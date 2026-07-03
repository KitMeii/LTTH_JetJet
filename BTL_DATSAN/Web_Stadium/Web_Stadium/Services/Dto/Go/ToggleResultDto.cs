namespace Web_Stadium.Services.Dto.Go
{
    /// <summary>Khop dung JSON that tra ve tu POST .../checkins/{playerId}/toggle (models.ToggleResult trong Go).</summary>
    public class ToggleResultDto
    {
        public bool Ok { get; set; }
        public int MatchId { get; set; }
        public int PlayerId { get; set; }
        public bool Checked { get; set; }
        public int TotalChecked { get; set; }
        public string? Message { get; set; }
        public DateTime SyncedAt { get; set; }
    }
}
