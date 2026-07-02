using Web_Stadium.EFCore;
using Web_Stadium.Services.JavaClient;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Kết quả pha xem trước lịch giải đấu — dùng để render `Views/Tournament/XemTruocLich.cshtml`
    /// và để so khớp lúc "Chốt lịch".
    ///
    /// KHÔNG ghi DB tại pha này. `MatchesRaw` là TranDau in-memory (chưa có Id thật);
    /// key = virtual matchId (index), map ngược lại từ payload client khi commit.
    /// </summary>
    public class PreviewLichResult
    {
        public GiaiDau Giai { get; set; } = null!;

        /// <summary>virtual matchId (0..N-1) → TranDau in-memory (chưa lưu DB).</summary>
        public Dictionary<int, TranDau> MatchesRaw { get; set; } = new();

        /// <summary>Cùng list trên nhưng dạng DTO (tên đội, vòng, bảng...) — đổ vào view + gửi Java validate.</summary>
        public List<MatchDto> MatchesDto { get; set; } = new();

        public List<SlotDto> AvailableSlots { get; set; } = new();
        public List<BookingConflictDto> Bookings { get; set; } = new();

        /// <summary>Assignments do Java gợi ý (best-effort).</summary>
        public List<AssignmentDto> Assignments { get; set; } = new();
        public List<int> Unassigned { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
