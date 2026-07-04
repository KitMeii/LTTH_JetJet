using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Sinh lịch vòng Knock-out (Tứ kết → Bán kết → Chung kết)
    /// dựa trên kết quả vòng bảng — lấy nhất/nhì mỗi bảng
    /// </summary>
    public class KnockOutService
    {
        private readonly SanBongContext _context;
        private readonly StandingService _standingService;

        public KnockOutService(SanBongContext context, StandingService standingService)
        {
            _context = context;
            _standingService = standingService;
        }

        // ══════════════════════════════════════════════════════════
        // Sinh toàn bộ vòng knock-out từ kết quả vòng bảng
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> SinhVongKnockOut(int giaiDauId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs)
                .Include(g => g.TranDaus)
                .FirstOrDefaultAsync(g => g.Id == giaiDauId);

            if (giai == null) return (false, "Không tìm thấy giải!");

            // Kiểm tra đã có vòng knock-out chưa
            var daCoKO = giai.TranDaus.Any(t => t.LoaiVong != "VongBang");
            if (daCoKO) return (false, "Vòng knock-out đã được sinh rồi!");

            // Lấy BXH từng bảng → lấy nhất/nhì
            var bxh = await _standingService.GetStandings(giaiDauId);
            var doiVao = new List<(DoiBong doi, int thuHang, string bang)>();

            foreach (var bang in giai.BangDaus)
            {
                if (!bxh.TryGetValue(bang.Id, out var rows) || rows.Count < 2)
                    return (false, $"{bang.TenBang} chưa đủ kết quả!");

                // Nhất bảng
                var nhat = giai.DoiBongs.FirstOrDefault(d => d.Id == rows[0].DoiId);
                var nhi = giai.DoiBongs.FirstOrDefault(d => d.Id == rows[1].DoiId);
                if (nhat == null || nhi == null)
                    return (false, $"Không xác định được đội từ {bang.TenBang}!");

                doiVao.Add((nhat, 1, bang.TenBang));
                doiVao.Add((nhi, 2, bang.TenBang));
            }

            // Xác định format dựa số đội vào knock-out
            int soDoiKO = doiVao.Count;
            var tranKO = new List<TranDau>();
            var ngayKO = giai.NgayKetThuc.AddDays(-21); // 3 tuần trước khi kết thúc

            if (soDoiKO == 8)
            {
                // 4 bảng × 2 = 8 đội → Tứ kết (4 trận) → Bán kết (2 trận) → CK (1 trận)
                tranKO.AddRange(SinhTuKet8Doi(doiVao, giaiDauId, ngayKO));
                tranKO.AddRange(SinhBanKetPlaceholder(giaiDauId, ngayKO.AddDays(7)));
                tranKO.Add(SinhChungKetPlaceholder(giaiDauId, ngayKO.AddDays(14)));
            }
            else if (soDoiKO == 4)
            {
                // 2 bảng × 2 = 4 đội → Bán kết (2 trận) → CK (1 trận)
                tranKO.AddRange(SinhBanKet4Doi(doiVao, giaiDauId, ngayKO));
                tranKO.Add(SinhChungKetPlaceholder(giaiDauId, ngayKO.AddDays(7)));
            }
            else if (soDoiKO == 2)
            {
                // 1 bảng chơi vòng tròn → 2 đội → Chung kết
                tranKO.Add(new TranDau
                {
                    GiaiDauId = giaiDauId,
                    DoiNhaId = doiVao[0].doi.Id,
                    DoiKhachId = doiVao[1].doi.Id,
                    VongDau = 99,
                    LoaiVong = "ChungKet",
                    NgayThiDau = ngayKO,
                    TrangThai = "Scheduled"
                });
            }
            else
            {
                return (false, $"Số đội vào knock-out ({soDoiKO}) không hợp lệ! Cần 2, 4 hoặc 8 đội.");
            }

            _context.TranDaus.AddRange(tranKO);
            await _context.SaveChangesAsync();

            return (true, "");
        }

        // ── 8 đội → 4 trận Tứ kết ─────────────────────────────────
        // Seeding: A1 vs D2, B1 vs C2, C1 vs B2, D1 vs A2
        private List<TranDau> SinhTuKet8Doi(
            List<(DoiBong doi, int thuHang, string bang)> doiVao,
            int giaiDauId, DateTime ngay)
        {
            // Lấy nhất/nhì từng bảng (A,B,C,D)
            var bangMap = doiVao.GroupBy(d => d.bang)
                .OrderBy(g => g.Key)
                .ToList();

            var result = new List<TranDau>();
            int n = bangMap.Count; // 4 bảng

            for (int i = 0; i < n / 2; i++)
            {
                var bangNhat = bangMap[i];
                var bangNhi = bangMap[n - 1 - i]; // cross-bracket

                var nhat1 = bangNhat.First(d => d.thuHang == 1).doi;
                var nhi2 = bangNhi.First(d => d.thuHang == 2).doi;
                var nhat2 = bangNhi.First(d => d.thuHang == 1).doi;
                var nhi1 = bangNhat.First(d => d.thuHang == 2).doi;

                result.Add(new TranDau
                {
                    GiaiDauId = giaiDauId,
                    DoiNhaId = nhat1.Id,
                    DoiKhachId = nhi2.Id,
                    VongDau = 10, // Tứ kết
                    LoaiVong = "TuKet",
                    NgayThiDau = ngay,
                    TrangThai = "Scheduled"
                });
                result.Add(new TranDau
                {
                    GiaiDauId = giaiDauId,
                    DoiNhaId = nhat2.Id,
                    DoiKhachId = nhi1.Id,
                    VongDau = 10,
                    LoaiVong = "TuKet",
                    NgayThiDau = ngay,
                    TrangThai = "Scheduled"
                });
            }

            return result;
        }

        // ── 4 đội → 2 trận Bán kết ───────────────────────────────
        private List<TranDau> SinhBanKet4Doi(
            List<(DoiBong doi, int thuHang, string bang)> doiVao,
            int giaiDauId, DateTime ngay)
        {
            var bangMap = doiVao.GroupBy(d => d.bang)
                .OrderBy(g => g.Key)
                .ToList();

            var nhat1 = bangMap[0].First(d => d.thuHang == 1).doi;
            var nhi2 = bangMap[1].First(d => d.thuHang == 2).doi;
            var nhat2 = bangMap[1].First(d => d.thuHang == 1).doi;
            var nhi1 = bangMap[0].First(d => d.thuHang == 2).doi;

            return new List<TranDau> {
                new TranDau {
                    GiaiDauId  = giaiDauId,
                    DoiNhaId   = nhat1.Id,
                    DoiKhachId = nhi2.Id,
                    VongDau    = 20,
                    LoaiVong   = "BanKet",
                    NgayThiDau = ngay,
                    TrangThai  = "Scheduled"
                },
                new TranDau {
                    GiaiDauId  = giaiDauId,
                    DoiNhaId   = nhat2.Id,
                    DoiKhachId = nhi1.Id,
                    VongDau    = 20,
                    LoaiVong   = "BanKet",
                    NgayThiDau = ngay,
                    TrangThai  = "Scheduled"
                }
            };
        }

        // ── Placeholder Bán kết (chờ kết quả Tứ kết) ────────────
        // Owner sẽ cập nhật đội sau khi có kết quả Tứ kết
        private List<TranDau> SinhBanKetPlaceholder(int giaiDauId, DateTime ngay)
        {
            // DoiNhaId/DoiKhachId = null → TBD (điền khi vòng trước Closed)
            return new List<TranDau> {
                new TranDau {
                    GiaiDauId  = giaiDauId,
                    DoiNhaId   = null,
                    DoiKhachId = null,
                    VongDau    = 20,
                    LoaiVong   = "BanKet",
                    NgayThiDau = ngay,
                    TrangThai  = "Pending" // Chờ xác định đội
                },
                new TranDau {
                    GiaiDauId  = giaiDauId,
                    DoiNhaId   = null,
                    DoiKhachId = null,
                    VongDau    = 20,
                    LoaiVong   = "BanKet",
                    NgayThiDau = ngay,
                    TrangThai  = "Pending"
                }
            };
        }

        // ── Placeholder Chung kết ─────────────────────────────────
        private TranDau SinhChungKetPlaceholder(int giaiDauId, DateTime ngay)
        {
            return new TranDau
            {
                GiaiDauId = giaiDauId,
                DoiNhaId = null,
                DoiKhachId = null,
                VongDau = 30,
                LoaiVong = "ChungKet",
                NgayThiDau = ngay,
                TrangThai = "Pending"
            };
        }

        // ══════════════════════════════════════════════════════════
        // Cập nhật đội cho trận knock-out sau khi có kết quả.
        // Gọi sau mỗi lần TranDau.TrangThai → Closed.
        //
        // Bracket cố định theo vị trí (không phụ thuộc thứ tự close):
        //   Tứ kết #0,#1 → Bán kết #0 (Nha, Khach)
        //   Tứ kết #2,#3 → Bán kết #1 (Nha, Khach)
        //   Bán kết #0,#1 → Chung kết (Nha, Khach)
        // Vị trí #i = index trong list các trận cùng LoaiVong sắp theo Id.
        // ══════════════════════════════════════════════════════════
        public async Task CapNhatDoiKnockOut(int tranDauId)
        {
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau).ThenInclude(g => g.TranDaus)
                .FirstOrDefaultAsync(t => t.Id == tranDauId);

            if (tran == null || tran.TrangThai != "Closed") return;
            if (!tran.BanThangNha.HasValue || !tran.BanThangKhach.HasValue) return;
            if (!tran.DoiNhaId.HasValue || !tran.DoiKhachId.HasValue) return;

            var vongTiepTheo = tran.LoaiVong switch
            {
                "TuKet" => "BanKet",
                "BanKet" => "ChungKet",
                _ => null
            };
            if (vongTiepTheo == null) return;

            // Đội thắng (hoà không xảy ra ở KO — nếu có thì lấy đội nhà cho chắc)
            int doiThang = tran.BanThangNha > tran.BanThangKhach
                ? tran.DoiNhaId.Value
                : tran.DoiKhachId.Value;

            // Vị trí (index) của trận nguồn trong list cùng LoaiVong
            var sameRound = tran.GiaiDau.TranDaus
                .Where(t => t.LoaiVong == tran.LoaiVong)
                .OrderBy(t => t.Id)
                .ToList();
            int srcIdx = sameRound.FindIndex(t => t.Id == tran.Id);
            if (srcIdx < 0) return;

            // Trận đích ở vòng kế: srcIdx / 2. Slot Nha nếu srcIdx chẵn, Khach nếu lẻ.
            var nextRound = tran.GiaiDau.TranDaus
                .Where(t => t.LoaiVong == vongTiepTheo)
                .OrderBy(t => t.Id)
                .ToList();
            int dstIdx = srcIdx / 2;
            if (dstIdx >= nextRound.Count) return;

            var dst = nextRound[dstIdx];
            if (srcIdx % 2 == 0) dst.DoiNhaId = doiThang;
            else dst.DoiKhachId = doiThang;

            // Nếu cả 2 slot đã có đội → chuyển từ Pending sang Scheduled
            if (dst.DoiNhaId.HasValue && dst.DoiKhachId.HasValue && dst.TrangThai == "Pending")
                dst.TrangThai = "Scheduled";

            await _context.SaveChangesAsync();
        }
    }
}