package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.StaffSanPhanCong;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

/** Chi doc — xem ghi chu trong model/StaffSanPhanCong.java. */
public interface StaffSanPhanCongRepository extends JpaRepository<StaffSanPhanCong, Integer> {

    List<StaffSanPhanCong> findByStaffId(Integer staffId);

    boolean existsByStaffIdAndSanBongId(Integer staffId, Integer sanBongId);
}
