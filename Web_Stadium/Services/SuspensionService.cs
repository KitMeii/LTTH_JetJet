using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Xử lý treo giò cầu thủ sau mỗi trận
    /// Logic: Thẻ đỏ → treo N trận / Tích lũy thẻ vàng → treo 1 trận
    /// </summary>
    public class SuspensionService
    {
        private readonly SanBongContext _context;
        public SuspensionService(SanBongContext context) => _context = context;

        // ══════════════════════════════════════════════════════════
        // Gọi sau mỗi trận Closed — kiểm tra và áp dụng treo giò
        // ══════════════════════════════════════════════════════════
        public async Task XuLyTreoGio(int giaiDauId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.DoiBongs).ThenInclude(d => d.ThanhViens)
                .FirstOrDefaultAsync(g => g.Id == giaiDauId);

            if (giai == null) return;

            // Lấy toàn bộ sự kiện thẻ của giải
            var suKiens = await _context.SuKienTrans
                .Include(s => s.TranDau)
                .Where(s => s.TranDau.GiaiDauId == giaiDauId
                         && s.ThanhVienId != null
                         && (s.LoaiSuKien == "TheVang"
                          || s.LoaiSuKien == "TheVangLan2"
                          || s.LoaiSuKien == "TheDo"))
                .ToListAsync();

            foreach (var tv in giai.DoiBongs.SelectMany(d => d.ThanhViens))
            {
                var suKienCuaTv = suKiens
                    .Where(s => s.ThanhVienId == tv.Id)
                    .ToList();

                // Treo giò vì thẻ đỏ trực tiếp
                var soTheDo = suKienCuaTv.Count(s => s.LoaiSuKien == "TheDo");

                // Treo giò vì tích lũy thẻ vàng
                var soTheVang = suKienCuaTv.Count(s =>
                    s.LoaiSuKien == "TheVang" || s.LoaiSuKien == "TheVangLan2");
                var treoDoVang = soTheVang / giai.SoTheVangTichLuy;

                var soTranTreo = soTheDo * giai.SoTranTreoGioTheDo + treoDoVang;

                // Cập nhật số trận còn bị cấm
                if (tv.SoTranTreoGio != soTranTreo)
                {
                    tv.SoTranTreoGio = soTranTreo;
                }
            }

            await _context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // Kiểm tra cầu thủ có bị treo giò ở trận cụ thể không
        // ══════════════════════════════════════════════════════════
        public bool BiTreoGio(ThanhVienDoi thanhVien, int soTranDaThiDau)
        {
            // Tính số trận đã thực sự thi đấu của cầu thủ
            // SoTranTreoGio = tổng số trận cần nghỉ tính từ đầu giải
            return thanhVien.SoTranTreoGio > soTranDaThiDau;
        }

        // ══════════════════════════════════════════════════════════
        // Lấy danh sách cầu thủ bị treo giò ở vòng kế tiếp
        // ══════════════════════════════════════════════════════════
        public async Task<List<ThanhVienDoi>> GetDanhSachTreoGio(
            int giaiDauId, int vongKeTiep)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.DoiBongs).ThenInclude(d => d.ThanhViens)
                .Include(g => g.TranDaus).ThenInclude(t => t.SuKiens)
                .FirstOrDefaultAsync(g => g.Id == giaiDauId);

            if (giai == null) return new();

            return giai.DoiBongs
                .SelectMany(d => d.ThanhViens)
                .Where(tv => tv.SoTranTreoGio >= vongKeTiep)
                .ToList();
        }
    }
}