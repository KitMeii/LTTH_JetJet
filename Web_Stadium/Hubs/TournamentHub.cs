using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Services;

namespace Web_Stadium.Hubs
{
    /// <summary>
    /// SignalR Hub cho Tournament
    /// Realtime: BXH, tỷ số trực tiếp, sự kiện trận đấu
    /// </summary>
    public class TournamentHub : Hub
    {
        private readonly SanBongContext _context;
        private readonly StandingService _standingService;

        public TournamentHub(SanBongContext context, StandingService standingService)
        {
            _context = context;
            _standingService = standingService;
        }

        // ── Client join vào group giải đấu ───────────────────────
        public async Task ThamGiaGiai(string giaiId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"giai_{giaiId}");
        }

        // ── Client join vào group trận đấu cụ thể ────────────────
        public async Task ThamGiaTran(string tranId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tran_{tranId}");
        }

        // ── Client rời group ─────────────────────────────────────
        public async Task RoiGiai(string giaiId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"giai_{giaiId}");
        }

        // ── Client yêu cầu BXH mới nhất ──────────────────────────
        public async Task LayBXH(int giaiId)
        {
            var bxh = await _standingService.GetStandings(giaiId);
            await Clients.Caller.SendAsync("NhanBXH", bxh);
        }

        // ══════════════════════════════════════════════════════════
        // SERVER → CLIENT methods (gọi từ Controller/Service)
        // ══════════════════════════════════════════════════════════

        // Broadcast BXH mới sau mỗi trận kết thúc
        public static async Task BroadcastBXH(
            IHubContext<TournamentHub> hubContext,
            int giaiId,
            Dictionary<int, List<StandingRow>> bxh)
        {
            await hubContext.Clients
                .Group($"giai_{giaiId}")
                .SendAsync("CapNhatBXH", bxh);
        }

        // Broadcast sự kiện trực tiếp (bàn thắng, thẻ)
        public static async Task BroadcastSuKien(
            IHubContext<TournamentHub> hubContext,
            int tranDauId,
            SuKienDto suKien)
        {
            await hubContext.Clients
                .Group($"tran_{tranDauId}")
                .SendAsync("SuKienMoi", suKien);
        }

        // Broadcast cập nhật tỷ số
        public static async Task BroadcastTyso(
            IHubContext<TournamentHub> hubContext,
            int giaiId,
            TysoDto tyso)
        {
            await hubContext.Clients
                .Group($"giai_{giaiId}")
                .SendAsync("CapNhatTyso", tyso);
        }

        // Broadcast trận kết thúc
        public static async Task BroadcastTranKetThuc(
            IHubContext<TournamentHub> hubContext,
            int giaiId,
            int tranDauId)
        {
            await hubContext.Clients
                .Group($"giai_{giaiId}")
                .SendAsync("TranKetThuc", new { tranDauId });
        }
    }

    // ── DTOs cho SignalR ──────────────────────────────────────────
    public class SuKienDto
    {
        public int TranDauId { get; set; }
        public int? ThanhVienId { get; set; }
        public string? TenCauThu { get; set; }
        public int? DoiId { get; set; }
        public string? TenDoi { get; set; }
        public string LoaiSuKien { get; set; } = "";
        public int? Phut { get; set; }
        public int TysoNha { get; set; }
        public int TysoKhach { get; set; }
    }

    public class TysoDto
    {
        public int TranDauId { get; set; }
        public int TysoNha { get; set; }
        public int TysoKhach { get; set; }
        public string TenNha { get; set; } = "";
        public string TenKhach { get; set; } = "";
    }
}