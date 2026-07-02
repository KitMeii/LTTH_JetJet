using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using Web_Stadium.EFCore;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Infrastructure concern: xuất Excel đối soát tài chính giải đấu
    /// Không thuộc domain — tách hoàn toàn
    /// </summary>
    public class TournamentExcelService
    {
        private readonly SanBongContext _context;
        public TournamentExcelService(SanBongContext context) => _context = context;

        // ══════════════════════════════════════════════════════════
        // Xuất file Excel 3 sheet: tổng hợp + thẻ phạt + kết quả
        // ══════════════════════════════════════════════════════════
        public async Task<(byte[] bytes, string fileName)> ExportDoiSoat(int giaiDauId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.DoiBongs).ThenInclude(d => d.ThanhViens)
                .Include(g => g.TranDaus).ThenInclude(t => t.SuKiens)
                    .ThenInclude(s => s.ThanhVien)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiNha)
                .Include(g => g.TranDaus).ThenInclude(t => t.DoiKhach)
                .FirstOrDefaultAsync(g => g.Id == giaiDauId)
                ?? throw new KeyNotFoundException("Không tìm thấy giải đấu!");

            using var wb = new XLWorkbook();

            BuildSheet1_TongHop(wb, giai);
            BuildSheet2_ThePhat(wb, giai);
            BuildSheet3_KetQua(wb, giai);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            var safeFileName = string.Concat(
                giai.TenGiai.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var fileName = $"DoiSoat_{safeFileName}_{DateTime.Now:yyyyMMdd}.xlsx";

            return (stream.ToArray(), fileName);
        }

        // ── Sheet 1: Tổng hợp từng đội ───────────────────────────
        private void BuildSheet1_TongHop(XLWorkbook wb, GiaiDau giai)
        {
            var ws = wb.AddWorksheet("Tổng hợp đội");

            // Title
            ws.Cell(1, 1).Value = $"GIẢI ĐẤU: {giai.TenGiai.ToUpper()}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(14, 168, 106);
            ws.Range(1, 1, 1, 9).Merge();

            ws.Cell(2, 1).Value = $"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 9).Merge();

            // Header
            var headers = new[] {
                "STT", "Tên đội", "Ký quỹ ban đầu",
                "Thẻ vàng", "Phạt vàng (đ)",
                "Thẻ đỏ",  "Phạt đỏ (đ)",
                "Tổng phạt", "Hoàn trả thực tế"
            };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(4, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(14, 168, 106);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data
            int row = 5, stt = 1;
            decimal tongHoanTra = 0;

            foreach (var doi in giai.DoiBongs.Where(d => d.DaThanhToan))
            {
                var suKienDoi = giai.TranDaus
                    .SelectMany(t => t.SuKiens)
                    .Where(s => s.DoiId == doi.Id).ToList();

                var soVang = suKienDoi.Count(s => s.LoaiSuKien is "TheVang" or "TheVangLan2");
                var soDo = suKienDoi.Count(s => s.LoaiSuKien == "TheDo");
                var phatVang = soVang * giai.TienPhatTheVang;
                var phatDo = soDo * giai.TienPhatTheDo;
                var tongPhat = phatVang + phatDo;
                var hoanTra = Math.Max(0, giai.TienKyQuy - tongPhat);
                tongHoanTra += hoanTra;

                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = doi.TenDoi;
                ws.Cell(row, 3).Value = (double)giai.TienKyQuy;
                ws.Cell(row, 4).Value = soVang;
                ws.Cell(row, 5).Value = (double)phatVang;
                ws.Cell(row, 6).Value = soDo;
                ws.Cell(row, 7).Value = (double)phatDo;
                ws.Cell(row, 8).Value = (double)tongPhat;
                ws.Cell(row, 9).Value = (double)hoanTra;

                // Tô màu theo mức độ phạt
                var rowStyle = ws.Row(row).Style;
                if (tongPhat >= giai.TienKyQuy)
                    rowStyle.Fill.BackgroundColor = XLColor.FromArgb(255, 220, 220);
                else if (tongPhat > 0)
                    rowStyle.Fill.BackgroundColor = XLColor.FromArgb(255, 248, 220);

                row++;
            }

            // Total row
            ws.Cell(row, 2).Value = "TỔNG HOÀN TRẢ";
            ws.Cell(row, 9).Value = (double)tongHoanTra;
            ws.Range(row, 1, row, 9).Style.Font.Bold = true;
            ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromArgb(220, 240, 230);

            ws.Columns().AdjustToContents();
            FormatCurrencyColumns(ws, new[] { 3, 5, 7, 8, 9 }, 4, row);
        }

        // ── Sheet 2: Chi tiết thẻ phạt ───────────────────────────
        private void BuildSheet2_ThePhat(XLWorkbook wb, GiaiDau giai)
        {
            var ws = wb.AddWorksheet("Chi tiết thẻ phạt");

            var headers = new[] { "Vòng", "Trận đấu", "Đội", "Cầu thủ", "Loại thẻ", "Phút", "Tiền phạt" };
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cell(1, c + 1).Value = headers[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
                ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(41, 98, 255);
                ws.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var tran in giai.TranDaus.OrderBy(t => t.VongDau))
            {
                var theEvents = tran.SuKiens
                    .Where(s => s.LoaiSuKien is "TheVang" or "TheDo" or "TheVangLan2")
                    .ToList();

                foreach (var sk in theEvents)
                {
                    var tenDoi = giai.DoiBongs.FirstOrDefault(d => d.Id == sk.DoiId)?.TenDoi ?? "?";
                    var tienPhat = sk.LoaiSuKien == "TheDo"
                        ? giai.TienPhatTheDo : giai.TienPhatTheVang;

                    ws.Cell(row, 1).Value = $"Vòng {tran.VongDau}";
                    ws.Cell(row, 2).Value = $"{tran.DoiNha?.TenDoi} vs {tran.DoiKhach?.TenDoi}";
                    ws.Cell(row, 3).Value = tenDoi;
                    ws.Cell(row, 4).Value = sk.ThanhVien?.HoTen ?? "?";
                    ws.Cell(row, 5).Value = sk.LoaiSuKien;
                    ws.Cell(row, 6).Value = sk.Phut?.ToString() ?? "-";
                    ws.Cell(row, 7).Value = (double)tienPhat;

                    // Màu thẻ
                    ws.Cell(row, 5).Style.Fill.BackgroundColor =
                        sk.LoaiSuKien == "TheDo"
                            ? XLColor.FromArgb(255, 100, 100)
                            : XLColor.FromArgb(255, 220, 100);

                    row++;
                }
            }

            ws.Columns().AdjustToContents();
        }

        // ── Sheet 3: Kết quả toàn giải ───────────────────────────
        private void BuildSheet3_KetQua(XLWorkbook wb, GiaiDau giai)
        {
            var ws = wb.AddWorksheet("Kết quả trận đấu");

            var headers = new[] { "Vòng", "Loại", "Đội nhà", "Tỷ số", "Đội khách", "Trạng thái" };
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cell(1, c + 1).Value = headers[c];
                ws.Cell(1, c + 1).Style.Font.Bold = true;
                ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(245, 158, 11);
                ws.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var t in giai.TranDaus.OrderBy(t => t.VongDau).ThenBy(t => t.NgayThiDau))
            {
                var tyso = t.BanThangNha.HasValue
                    ? $"{t.BanThangNha} - {t.BanThangKhach}" : "Chưa đấu";

                ws.Cell(row, 1).Value = t.VongDau;
                ws.Cell(row, 2).Value = t.LoaiVong;
                ws.Cell(row, 3).Value = t.DoiNha?.TenDoi ?? "?";
                ws.Cell(row, 4).Value = tyso;
                ws.Cell(row, 5).Value = t.DoiKhach?.TenDoi ?? "?";
                ws.Cell(row, 6).Value = t.TrangThai;

                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 4).Style.Font.Bold = t.TrangThai == "Closed";

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // ── Format tiền tệ ───────────────────────────────────────
        private void FormatCurrencyColumns(
            IXLWorksheet ws, int[] cols, int startRow, int endRow)
        {
            foreach (var col in cols)
                ws.Range(startRow, col, endRow, col)
                  .Style.NumberFormat.Format = "#,##0";
        }
    }
}