package com.pitchhub.report.controller;

import com.lowagie.text.*;
import com.lowagie.text.pdf.PdfPCell;
import com.lowagie.text.pdf.PdfPTable;
import com.lowagie.text.pdf.PdfWriter;
import com.pitchhub.report.dto.ReportData;
import com.pitchhub.report.dto.ReportDetail;
import com.pitchhub.report.util.FontHelper;
import org.springframework.http.ContentDisposition;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.awt.Color;
import java.io.ByteArrayOutputStream;
import java.text.NumberFormat;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.Locale;

@RestController
@RequestMapping("/api/report")
@CrossOrigin(origins = "*")
public class ReportController {

    private static final NumberFormat VN = NumberFormat.getInstance(new Locale("vi", "VN"));

    @PostMapping("/pdf")
    public ResponseEntity<byte[]> generatePdf(@RequestBody ReportData data) {
        try {
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            Document document = new Document(PageSize.A4, 40, 40, 50, 40);
            PdfWriter.getInstance(document, out);
            document.open();

            Font titleFont  = FontHelper.font(18, Font.BOLD);
            Font subFont    = FontHelper.font(11, Font.ITALIC);
            Font boldFont   = FontHelper.font(12, Font.BOLD);
            Font normalFont = FontHelper.font(11, Font.NORMAL);
            Color headerBg  = new Color(15, 110, 74);

            Paragraph title = new Paragraph(safe(data.getTitle(), "Báo cáo"), titleFont);
            title.setAlignment(Element.ALIGN_CENTER);
            document.add(title);

            String createdAt = LocalDateTime.now().format(DateTimeFormatter.ofPattern("dd/MM/yyyy HH:mm"));
            Paragraph created = new Paragraph("Xuất lúc: " + createdAt, subFont);
            created.setAlignment(Element.ALIGN_CENTER);
            document.add(created);
            document.add(Chunk.NEWLINE);

            document.add(new Paragraph("Sân: " + safe(data.getStadiumName(), "—"), normalFont));
            document.add(new Paragraph("Kỳ báo cáo: " + safe(data.getPeriod(), "—"), normalFont));
            document.add(Chunk.NEWLINE);

            if (data.getSummary() != null) {
                Paragraph sumTitle = new Paragraph("Tóm tắt", boldFont);
                sumTitle.setSpacingAfter(6f);
                document.add(sumTitle);

                PdfPTable summaryTable = new PdfPTable(2);
                summaryTable.setWidthPercentage(100);
                summaryTable.setWidths(new float[]{2f, 3f});

                addHeader(summaryTable, "Chỉ tiêu", boldFont, headerBg);
                addHeader(summaryTable, "Giá trị",  boldFont, headerBg);

                addRow(summaryTable, "Tổng doanh thu",  formatVnd(data.getSummary().getTotalRevenue()), normalFont);
                addRow(summaryTable, "Tổng lượt đặt",   VN.format(data.getSummary().getTotalBookings()), normalFont);
                addRow(summaryTable, "Trung bình / đơn", formatVnd(data.getSummary().getAvgPerBooking()), normalFont);
                addRow(summaryTable, "Hoa hồng",        formatVnd(data.getSummary().getCommission()), normalFont);
                addRow(summaryTable, "Thu thuần",       formatVnd(data.getSummary().getNetRevenue()), normalFont);
                document.add(summaryTable);
                document.add(Chunk.NEWLINE);
            }

            if (data.getDetails() != null && !data.getDetails().isEmpty()) {
                Paragraph detailTitle = new Paragraph("Chi tiết theo kỳ", boldFont);
                detailTitle.setSpacingAfter(6f);
                document.add(detailTitle);

                PdfPTable detailTable = new PdfPTable(4);
                detailTable.setWidthPercentage(100);
                detailTable.setWidths(new float[]{2f, 3f, 1.5f, 3f});
                detailTable.setHeaderRows(1);

                addHeader(detailTable, "Kỳ",              boldFont, headerBg);
                addHeader(detailTable, "Doanh thu (VND)", boldFont, headerBg);
                addHeader(detailTable, "Số đơn",          boldFont, headerBg);
                addHeader(detailTable, "Thu thuần (VND)", boldFont, headerBg);

                int i = 0;
                for (ReportDetail d : data.getDetails()) {
                    Color bg = (i++ % 2 == 0) ? Color.WHITE : new Color(248, 250, 252);
                    addCell(detailTable, safe(d.getLabel(), ""),       normalFont, bg, Element.ALIGN_LEFT);
                    addCell(detailTable, VN.format(Math.round(d.getTotalRevenue())), normalFont, bg, Element.ALIGN_RIGHT);
                    addCell(detailTable, String.valueOf(d.getBookings()),            normalFont, bg, Element.ALIGN_CENTER);
                    addCell(detailTable, VN.format(Math.round(d.getNetRevenue())),   normalFont, bg, Element.ALIGN_RIGHT);
                }
                document.add(detailTable);
            }

            document.close();
            byte[] pdfBytes = out.toByteArray();

            HttpHeaders headers = new HttpHeaders();
            headers.setContentType(MediaType.APPLICATION_PDF);
            headers.setContentDisposition(
                ContentDisposition.attachment().filename("baocao.pdf").build()
            );
            return new ResponseEntity<>(pdfBytes, headers, HttpStatus.OK);
        } catch (Exception e) {
            e.printStackTrace();
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                    .body(("Lỗi sinh PDF: " + e.getMessage()).getBytes());
        }
    }

    private static String safe(String s, String fallback) {
        return (s == null || s.isBlank()) ? fallback : s;
    }

    private static String formatVnd(double value) {
        return VN.format(Math.round(value)) + " VND";
    }

    private static void addHeader(PdfPTable table, String text, Font font, Color bg) {
        PdfPCell cell = new PdfPCell(new Phrase(text, new Font(font.getBaseFont(), font.getSize(), Font.BOLD, Color.WHITE)));
        cell.setBackgroundColor(bg);
        cell.setHorizontalAlignment(Element.ALIGN_CENTER);
        cell.setPadding(7f);
        table.addCell(cell);
    }

    private static void addRow(PdfPTable table, String label, String value, Font font) {
        PdfPCell l = new PdfPCell(new Phrase(label, font));
        l.setPadding(6f);
        table.addCell(l);
        PdfPCell v = new PdfPCell(new Phrase(value, font));
        v.setPadding(6f);
        v.setHorizontalAlignment(Element.ALIGN_RIGHT);
        table.addCell(v);
    }

    private static void addCell(PdfPTable table, String text, Font font, Color bg, int align) {
        PdfPCell c = new PdfPCell(new Phrase(text, font));
        c.setBackgroundColor(bg);
        c.setHorizontalAlignment(align);
        c.setPadding(5f);
        table.addCell(c);
    }
}
