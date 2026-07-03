package com.pitchhub.recommendation.service;

import com.pitchhub.recommendation.entity.DatSan;
import com.pitchhub.recommendation.repository.DatSanRepository;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;
import java.math.BigDecimal;
import java.math.RoundingMode;
import java.time.LocalDateTime;
import java.util.List;

@Service
public class PricingSuggestionService {

    @Autowired
    private DatSanRepository repository;

    public SuggestionResult suggestPrice(Integer sanId, Integer month, Integer year) {
        LocalDateTime start = LocalDateTime.of(year, month, 1, 0, 0);
        LocalDateTime end = start.plusMonths(1);

        List<DatSan> bookings = repository.findSuccessfulBookingsBetween(start, end);

        if (bookings.isEmpty()) {
            return new SuggestionResult(350000, 250000,
                "Chưa có dữ liệu lịch sử, đề xuất giá mặc định.", 0.5);
        }

        BigDecimal total = bookings.stream()
                .map(DatSan::getTongTien)
                .reduce(BigDecimal.ZERO, BigDecimal::add);
        BigDecimal avg = total.divide(BigDecimal.valueOf(bookings.size()), RoundingMode.HALF_UP);

        int goldPrice = avg.multiply(BigDecimal.valueOf(1.2)).intValue();
        int regularPrice = avg.multiply(BigDecimal.valueOf(0.9)).intValue();

        // Đảm bảo giá không quá thấp
        if (regularPrice < 200000) {
            regularPrice = 200000;
            goldPrice = 300000;
        }

        String reason = String.format(
            "Dựa trên %d đơn đặt thành công trong tháng %d/%d, giá trung bình %,d VND. Đề xuất tăng 20%% cho giờ vàng.",
            bookings.size(), month, year, avg.intValue());

        return new SuggestionResult(goldPrice, regularPrice, reason, 0.85);
    }

    public record SuggestionResult(int suggestedGoldPrice, int suggestedRegularPrice, String reason, double confidence) {}
}