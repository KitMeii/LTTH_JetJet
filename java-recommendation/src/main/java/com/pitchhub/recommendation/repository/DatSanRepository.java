package com.pitchhub.recommendation.repository;

import com.pitchhub.recommendation.entity.DatSan;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import java.time.LocalDateTime;
import java.util.List;

public interface DatSanRepository extends JpaRepository<DatSan, Integer> {
    @Query("SELECT d FROM DatSan d WHERE d.trangThai IN ('DaXacNhan','HoanThanh') AND d.ngayThiDau BETWEEN :start AND :end")
    List<DatSan> findSuccessfulBookingsBetween(@Param("start") LocalDateTime start, @Param("end") LocalDateTime end);
}