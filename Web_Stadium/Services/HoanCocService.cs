using Web_Stadium.EFCore;
using Microsoft.EntityFrameworkCore;

namespace Web_Stadium.Services
{
    public class HoanCocService
    {
        private readonly SanBongContext _context;
        private readonly ILogger<HoanCocService> _logger;

        public HoanCocService(SanBongContext context, ILogger<HoanCocService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool success, string message)> ThucHienHoanCocAsync(
            DatSan datSan,
            string nguonHuy,
            string vaiTroNguoiKhoiTao,
            int? nguoiKhoiTaoId,
            decimal? soTienHoanTuyChon = null,
            string? ghiChu = null)
        {
            if (datSan.TrangThai == "DaHuy")
            {
                return (false, "Đơn đã bị hủy trước đó, không thể hoàn cọc lại.");
            }

            var san = await _context.SanBongs.FindAsync(datSan.KhungGio.SanBongId);
            if (san == null)
            {
                return (false, "Không tìm thấy thông tin sân.");
            }

            decimal phanTramHoan;
            string loaiHoanCoc;
            decimal soTienHoan;

            if (nguonHuy == "OwnerSuCo")
            {
                phanTramHoan = 1.00m;
                loaiHoanCoc = "SuCo";
                soTienHoan = datSan.TienCoc;
            }
            else if (nguonHuy == "OwnerKhieuNai")
            {
                if (soTienHoanTuyChon == null || soTienHoanTuyChon < 0 || soTienHoanTuyChon > datSan.TienCoc)
                {
                    return (false, "Số tiền hoàn không hợp lệ.");
                }
                soTienHoan = soTienHoanTuyChon.Value;
                phanTramHoan = datSan.TienCoc > 0 ? soTienHoan / datSan.TienCoc : 0;
                loaiHoanCoc = "KhieuNai";
            }
            else
            {
                var mocHuy = datSan.NgayThiDau.AddMinutes(-san.ThoiGianHuyTruocGioDa);
                var dungHan = DateTime.Now <= mocHuy;

                if (dungHan)
                {
                    phanTramHoan = san.PhanTramHoanCocDungHan;
                    loaiHoanCoc = "DungHan";
                }
                else
                {
                    phanTramHoan = san.PhanTramHoanCocTreHan;
                    loaiHoanCoc = "TreHan";
                }

                soTienHoan = datSan.TienCoc * phanTramHoan;
            }

            datSan.NguonHuy = nguonHuy;
            datSan.LoaiHoanCoc = loaiHoanCoc;
            datSan.PhanTramHoan = phanTramHoan;
            datSan.SoTienDaHoan = soTienHoan;

            var giaoDich = new GiaoDichHoanCoc
            {
                DatSanId = datSan.Id,
                ThoiGianGiaoDich = DateTime.Now,
                SoTien = soTienHoan,
                VaiTroNguoiKhoiTao = vaiTroNguoiKhoiTao,
                NguoiKhoiTaoId = nguoiKhoiTaoId,
                TrangThaiHoan = "DaGhiNhan",
                GhiChu = ghiChu
            };

            _context.GiaoDichHoanCocs.Add(giaoDich);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Hoàn cọc OK | DatSanId={Id} | Nguồn={Nguon} | Số tiền={Tien}",
                    datSan.Id, nguonHuy, soTienHoan);
                return (true, $"Hoàn cọc thành công: {soTienHoan:N0} VNĐ ({phanTramHoan:P0})");
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Lỗi hoàn cọc | DatSanId={Id}: {Msg}", datSan.Id, ex.Message);
                return (false, "Có lỗi xảy ra khi hoàn cọc.");
            }
        }
    }
}
