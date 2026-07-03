package com.pitchhub.tournament.service;

import com.pitchhub.tournament.model.DoiBong;
import com.pitchhub.tournament.model.GiaiDau;
import com.pitchhub.tournament.model.SuKienTran;
import com.pitchhub.tournament.model.TranDau;
import com.pitchhub.tournament.repository.GiaiDauRepository;
import org.apache.poi.ss.usermodel.*;
import org.apache.poi.xssf.usermodel.XSSFWorkbook;
import org.springframework.stereotype.Service;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.math.BigDecimal;
import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.NoSuchElementException;

/**
 * Infrastructure concern: xuat Excel doi soat tai chinh giai dau.
 * Port tu TournamentExcelService.cs — dung Apache POI thay ClosedXML.
 */
@Service
public class TournamentExcelService {

    private final GiaiDauRepository giaiDauRepository;

    public TournamentExcelService(GiaiDauRepository giaiDauRepository) {
        this.giaiDauRepository = giaiDauRepository;
    }

    public record ExcelFile(byte[] bytes, String fileName) {
    }

    public ExcelFile exportDoiSoat(Integer giaiDauId) {
        GiaiDau giai = giaiDauRepository.findDetailById(giaiDauId)
                .orElseThrow(() -> new NoSuchElementException("Khong tim thay giai dau!"));

        try (XSSFWorkbook wb = new XSSFWorkbook()) {
            buildSheet1TongHop(wb, giai);
            buildSheet2ThePhat(wb, giai);
            buildSheet3KetQua(wb, giai);

            try (ByteArrayOutputStream out = new ByteArrayOutputStream()) {
                wb.write(out);
                String safeFileName = giai.getTenGiai().replaceAll("[\\\\/:*?\"<>|]", "_");
                String fileName = "DoiSoat_" + safeFileName + "_"
                        + java.time.LocalDate.now().format(DateTimeFormatter.ofPattern("yyyyMMdd")) + ".xlsx";
                return new ExcelFile(out.toByteArray(), fileName);
            }
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    // ── Sheet 1: Tong hop tung doi ───────────────────────────
    private void buildSheet1TongHop(XSSFWorkbook wb, GiaiDau giai) {
        Sheet ws = wb.createSheet("Tong hop doi");

        CellStyle titleStyle = wb.createCellStyle();
        Font titleFont = wb.createFont();
        titleFont.setBold(true);
        titleFont.setFontHeightInPoints((short) 14);
        titleFont.setColor(IndexedColors.DARK_GREEN.getIndex());
        titleStyle.setFont(titleFont);

        Row titleRow = ws.createRow(0);
        Cell titleCell = titleRow.createCell(0);
        titleCell.setCellValue("GIAI DAU: " + giai.getTenGiai().toUpperCase());
        titleCell.setCellStyle(titleStyle);

        CellStyle headerStyle = headerStyle(wb, IndexedColors.DARK_GREEN);
        String[] headers = {"STT", "Ten doi", "Ky quy ban dau", "The vang", "Phat vang (d)",
                "The do", "Phat do (d)", "Tong phat", "Hoan tra thuc te"};
        Row headerRow = ws.createRow(3);
        for (int c = 0; c < headers.length; c++) {
            Cell cell = headerRow.createCell(c);
            cell.setCellValue(headers[c]);
            cell.setCellStyle(headerStyle);
        }

        int rowIdx = 4, stt = 1;
        BigDecimal tongHoanTra = BigDecimal.ZERO;

        for (DoiBong doi : giai.getDoiBongs()) {
            if (!Boolean.TRUE.equals(doi.getDaThanhToan())) continue;

            List<SuKienTran> suKienDoi = giai.getTranDaus().stream()
                    .flatMap(t -> t.getSuKiens().stream())
                    .filter(s -> doi.getId().equals(s.getDoiId()))
                    .toList();

            long soVang = suKienDoi.stream().filter(s -> "TheVang".equals(s.getLoaiSuKien()) || "TheVangLan2".equals(s.getLoaiSuKien())).count();
            long soDo = suKienDoi.stream().filter(s -> "TheDo".equals(s.getLoaiSuKien())).count();
            BigDecimal phatVang = giai.getTienPhatTheVang().multiply(BigDecimal.valueOf(soVang));
            BigDecimal phatDo = giai.getTienPhatTheDo().multiply(BigDecimal.valueOf(soDo));
            BigDecimal tongPhat = phatVang.add(phatDo);
            BigDecimal hoanTra = giai.getTienKyQuy().subtract(tongPhat).max(BigDecimal.ZERO);
            tongHoanTra = tongHoanTra.add(hoanTra);

            Row row = ws.createRow(rowIdx);
            row.createCell(0).setCellValue(stt++);
            row.createCell(1).setCellValue(doi.getTenDoi());
            row.createCell(2).setCellValue(giai.getTienKyQuy().doubleValue());
            row.createCell(3).setCellValue(soVang);
            row.createCell(4).setCellValue(phatVang.doubleValue());
            row.createCell(5).setCellValue(soDo);
            row.createCell(6).setCellValue(phatDo.doubleValue());
            row.createCell(7).setCellValue(tongPhat.doubleValue());
            row.createCell(8).setCellValue(hoanTra.doubleValue());

            if (tongPhat.compareTo(giai.getTienKyQuy()) >= 0) {
                toMauDong(wb, row, IndexedColors.ROSE);
            } else if (tongPhat.compareTo(BigDecimal.ZERO) > 0) {
                toMauDong(wb, row, IndexedColors.LIGHT_YELLOW);
            }

            rowIdx++;
        }

        CellStyle boldStyle = wb.createCellStyle();
        Font boldFont = wb.createFont();
        boldFont.setBold(true);
        boldStyle.setFont(boldFont);

        Row totalRow = ws.createRow(rowIdx);
        Cell tongLabel = totalRow.createCell(1);
        tongLabel.setCellValue("TONG HOAN TRA");
        tongLabel.setCellStyle(boldStyle);
        Cell tongValue = totalRow.createCell(8);
        tongValue.setCellValue(tongHoanTra.doubleValue());
        tongValue.setCellStyle(boldStyle);

        for (int c = 0; c < headers.length; c++) ws.autoSizeColumn(c);
    }

    // ── Sheet 2: Chi tiet the phat ───────────────────────────
    private void buildSheet2ThePhat(XSSFWorkbook wb, GiaiDau giai) {
        Sheet ws = wb.createSheet("Chi tiet the phat");

        CellStyle headerStyle = headerStyle(wb, IndexedColors.BLUE);
        String[] headers = {"Vong", "Tran dau", "Doi", "Cau thu", "Loai the", "Phut", "Tien phat"};
        Row headerRow = ws.createRow(0);
        for (int c = 0; c < headers.length; c++) {
            Cell cell = headerRow.createCell(c);
            cell.setCellValue(headers[c]);
            cell.setCellStyle(headerStyle);
        }

        int rowIdx = 1;
        List<TranDau> tranDaus = giai.getTranDaus().stream()
                .sorted((a, b) -> Integer.compare(a.getVongDau(), b.getVongDau())).toList();

        for (TranDau tran : tranDaus) {
            List<SuKienTran> theEvents = tran.getSuKiens().stream()
                    .filter(s -> "TheVang".equals(s.getLoaiSuKien()) || "TheDo".equals(s.getLoaiSuKien())
                            || "TheVangLan2".equals(s.getLoaiSuKien()))
                    .toList();

            for (SuKienTran sk : theEvents) {
                String tenDoi = giai.getDoiBongs().stream()
                        .filter(d -> d.getId().equals(sk.getDoiId())).findFirst()
                        .map(DoiBong::getTenDoi).orElse("?");
                BigDecimal tienPhat = "TheDo".equals(sk.getLoaiSuKien()) ? giai.getTienPhatTheDo() : giai.getTienPhatTheVang();

                Row row = ws.createRow(rowIdx++);
                row.createCell(0).setCellValue("Vong " + tran.getVongDau());
                row.createCell(1).setCellValue(tenDoiAn(tran.getDoiNha()) + " vs " + tenDoiAn(tran.getDoiKhach()));
                row.createCell(2).setCellValue(tenDoi);
                row.createCell(3).setCellValue(sk.getThanhVien() != null ? sk.getThanhVien().getHoTen() : "?");
                row.createCell(4).setCellValue(sk.getLoaiSuKien());
                row.createCell(5).setCellValue(sk.getPhut() != null ? sk.getPhut().toString() : "-");
                row.createCell(6).setCellValue(tienPhat.doubleValue());
            }
        }

        for (int c = 0; c < headers.length; c++) ws.autoSizeColumn(c);
    }

    // ── Sheet 3: Ket qua toan giai ───────────────────────────
    private void buildSheet3KetQua(XSSFWorkbook wb, GiaiDau giai) {
        Sheet ws = wb.createSheet("Ket qua tran dau");

        CellStyle headerStyle = headerStyle(wb, IndexedColors.ORANGE);
        String[] headers = {"Vong", "Loai", "Doi nha", "Ty so", "Doi khach", "Trang thai"};
        Row headerRow = ws.createRow(0);
        for (int c = 0; c < headers.length; c++) {
            Cell cell = headerRow.createCell(c);
            cell.setCellValue(headers[c]);
            cell.setCellStyle(headerStyle);
        }

        int rowIdx = 1;
        List<TranDau> tranDaus = giai.getTranDaus().stream()
                .sorted((a, b) -> {
                    int cmp = Integer.compare(a.getVongDau(), b.getVongDau());
                    return cmp != 0 ? cmp : a.getNgayThiDau().compareTo(b.getNgayThiDau());
                }).toList();

        for (TranDau t : tranDaus) {
            String tyso = t.getBanThangNha() != null ? t.getBanThangNha() + " - " + t.getBanThangKhach() : "Chua dau";

            Row row = ws.createRow(rowIdx++);
            row.createCell(0).setCellValue(t.getVongDau());
            row.createCell(1).setCellValue(t.getLoaiVong());
            row.createCell(2).setCellValue(tenDoiAn(t.getDoiNha()));
            row.createCell(3).setCellValue(tyso);
            row.createCell(4).setCellValue(tenDoiAn(t.getDoiKhach()));
            row.createCell(5).setCellValue(t.getTrangThai());
        }

        for (int c = 0; c < headers.length; c++) ws.autoSizeColumn(c);
    }

    private String tenDoiAn(DoiBong doi) {
        return doi != null ? doi.getTenDoi() : "?";
    }

    private CellStyle headerStyle(XSSFWorkbook wb, IndexedColors color) {
        CellStyle style = wb.createCellStyle();
        Font font = wb.createFont();
        font.setBold(true);
        font.setColor(IndexedColors.WHITE.getIndex());
        style.setFont(font);
        style.setFillForegroundColor(color.getIndex());
        style.setFillPattern(FillPatternType.SOLID_FOREGROUND);
        style.setAlignment(HorizontalAlignment.CENTER);
        return style;
    }

    private void toMauDong(XSSFWorkbook wb, Row row, IndexedColors color) {
        CellStyle style = wb.createCellStyle();
        style.setFillForegroundColor(color.getIndex());
        style.setFillPattern(FillPatternType.SOLID_FOREGROUND);
        row.forEach(cell -> cell.setCellStyle(style));
    }
}
