using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Tính bảng xếp hạng theo chuẩn FIFA tie-breakers
    /// View chỉ render, không tính toán
    /// </summary>
    public class StandingService
    {
        private readonly SanBongContext _context;
        public StandingService(SanBongContext context) => _context = context;

        // ══════════════════════════════════════════════════════════
        // Lấy BXH toàn giải — trả về dict {bangId: [rows]}
        // ══════════════════════════════════════════════════════════
        public async Task<Dictionary<int, List<StandingRow>>> GetStandings(int giaiDauId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs)
                .Include(g => g.TranDaus)
                .FirstOrDefaultAsync(g => g.Id == giaiDauId);

            if (giai == null) return new();

            var result = new Dictionary<int, List<StandingRow>>();

            foreach (var bang in giai.BangDaus)
            {
                var doiTrongBang = giai.DoiBongs
                    .Where(d => d.BangId == bang.Id).ToList();

                var stats = doiTrongBang.ToDictionary(d => d.Id, d => new StandingRow
                {
                    DoiId = d.Id,
                    TenDoi = d.TenDoi,
                    LogoUrl = d.LogoUrl
                });

                // Tính từ các trận đã Closed
                var transClosed = giai.TranDaus
                    .Where(t => t.BangId == bang.Id && t.TrangThai == "Closed")
                    .ToList();

                foreach (var tran in transClosed)
                {
                    if (!tran.BanThangNha.HasValue || !tran.BanThangKhach.HasValue) continue;
                    if (!stats.ContainsKey(tran.DoiNhaId) ||
                        !stats.ContainsKey(tran.DoiKhachId)) continue;

                    var nha = stats[tran.DoiNhaId];
                    var khach = stats[tran.DoiKhachId];

                    nha.SoTran++; khach.SoTran++;
                    nha.BanThang += tran.BanThangNha.Value;
                    nha.BanThua += tran.BanThangKhach.Value;
                    khach.BanThang += tran.BanThangKhach.Value;
                    khach.BanThua += tran.BanThangNha.Value;

                    if (tran.BanThangNha > tran.BanThangKhach)
                    {
                        nha.Thang++; nha.Diem += 3; khach.Thua++;
                    }
                    else if (tran.BanThangNha < tran.BanThangKhach)
                    {
                        khach.Thang++; khach.Diem += 3; nha.Thua++;
                    }
                    else
                    {
                        nha.Hoa++; nha.Diem++;
                        khach.Hoa++; khach.Diem++;
                    }
                }

                // FIFA Tie-breakers:
                // 1. Điểm → 2. Đối đầu trực tiếp → 3. Hiệu số → 4. Tổng bàn thắng
                var sorted = SapXepFIFA(stats.Values.ToList(), giai.TranDaus
                    .Where(t => t.BangId == bang.Id && t.TrangThai == "Closed").ToList());

                for (int i = 0; i < sorted.Count; i++)
                    sorted[i].ThuHang = i + 1;

                result[bang.Id] = sorted;
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════
        // FIFA tie-breaker sắp xếp
        // ══════════════════════════════════════════════════════════
        private List<StandingRow> SapXepFIFA(
            List<StandingRow> rows, List<TranDau> tatCaTran)
        {
            // Group các đội cùng điểm
            var groups = rows.GroupBy(r => r.Diem)
                             .OrderByDescending(g => g.Key)
                             .ToList();

            var result = new List<StandingRow>();

            foreach (var group in groups)
            {
                var doiCungDiem = group.ToList();

                if (doiCungDiem.Count == 1)
                {
                    result.AddRange(doiCungDiem);
                    continue;
                }

                // Tính đối đầu trực tiếp trong nhóm
                var doiIds = doiCungDiem.Select(d => d.DoiId).ToHashSet();
                var tranDoiDau = tatCaTran.Where(t =>
                    doiIds.Contains(t.DoiNhaId) &&
                    doiIds.Contains(t.DoiKhachId)).ToList();

                var doiDauStats = doiCungDiem.ToDictionary(d => d.DoiId, _ => new {
                    diem = 0,
                    hieuSo = 0,
                    banThang = 0
                });

                // Tính điểm đối đầu trực tiếp
                var ddStats = doiCungDiem.ToDictionary(d => d.DoiId,
                    _ => new StandingSubRow());

                foreach (var t in tranDoiDau)
                {
                    if (!t.BanThangNha.HasValue) continue;
                    var nha = ddStats[t.DoiNhaId];
                    var khach = ddStats[t.DoiKhachId];

                    nha.BanThang += t.BanThangNha.Value;
                    nha.BanThua += t.BanThangKhach.Value;
                    khach.BanThang += t.BanThangKhach.Value;
                    khach.BanThua += t.BanThangNha.Value;

                    if (t.BanThangNha > t.BanThangKhach)
                    { nha.Diem += 3; }
                    else if (t.BanThangNha < t.BanThangKhach)
                    { khach.Diem += 3; }
                    else
                    { nha.Diem++; khach.Diem++; }
                }

                // Sắp xếp theo: Điểm ĐĐ → Hiệu số ĐĐ → BT ĐĐ → Hiệu số chung → BT chung
                var sorted = doiCungDiem.OrderByDescending(d => ddStats[d.DoiId].Diem)
                    .ThenByDescending(d => ddStats[d.DoiId].HieuSo)
                    .ThenByDescending(d => ddStats[d.DoiId].BanThang)
                    .ThenByDescending(d => d.HieuSo)
                    .ThenByDescending(d => d.BanThang)
                    .ToList();

                result.AddRange(sorted);
            }

            return result;
        }

        private class StandingSubRow
        {
            public int Diem { get; set; }
            public int BanThang { get; set; }
            public int BanThua { get; set; }
            public int HieuSo => BanThang - BanThua;
        }
    }

    // ── Public DTO dùng ở View ────────────────────────────────
    public class StandingRow
    {
        public int DoiId { get; set; }
        public string TenDoi { get; set; } = "";
        public string? LogoUrl { get; set; }
        public int ThuHang { get; set; }
        public int SoTran { get; set; }
        public int Thang { get; set; }
        public int Hoa { get; set; }
        public int Thua { get; set; }
        public int BanThang { get; set; }
        public int BanThua { get; set; }
        public int Diem { get; set; }
        public int HieuSo => BanThang - BanThua;
    }
}