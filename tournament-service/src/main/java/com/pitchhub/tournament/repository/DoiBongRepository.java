package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.DoiBong;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.LocalDateTime;
import java.util.List;
import java.util.Optional;

public interface DoiBongRepository extends JpaRepository<DoiBong, Integer> {

    // LEFT JOIN FETCH bang + thanhViens — DoiBongResponse.from() doc ca hai ngay
    // sau khi tra ve (controller khong mo transaction), tranh LazyInitializationException.
    // "distinct" bat buoc vi fetch join tren collection (thanhViens) nhan ban dong
    // DoiBong theo so luong thanh vien — thieu se lam sai cac cho dung .size().
    @Query("select distinct d from DoiBong d left join fetch d.bang left join fetch d.thanhViens where d.giaiDauId = :giaiDauId")
    List<DoiBong> findByGiaiDauId(@Param("giaiDauId") Integer giaiDauId);

    List<DoiBong> findByGiaiDauIdAndDaThanhToan(Integer giaiDauId, Boolean daThanhToan);

    long countByGiaiDauIdAndDaThanhToan(Integer giaiDauId, Boolean daThanhToan);

    boolean existsByGiaiDauIdAndTenDoiIgnoreCase(Integer giaiDauId, String tenDoi);

    boolean existsByGiaiDauIdAndDoiTruongId(Integer giaiDauId, Integer doiTruongId);

    Optional<DoiBong> findByIdAndDoiTruongId(Integer id, Integer doiTruongId);

    List<DoiBong> findByThoiGianThanhToanBetween(LocalDateTime from, LocalDateTime to);
}
