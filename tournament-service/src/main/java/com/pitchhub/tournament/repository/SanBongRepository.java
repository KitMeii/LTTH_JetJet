package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.SanBong;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

/** Chi doc — xem ghi chu trong model/SanBong.java. */
public interface SanBongRepository extends JpaRepository<SanBong, Integer> {

    List<SanBong> findByOwnerIdAndTrangThaiDuyetAndIsHidden(
            Integer ownerId, String trangThaiDuyet, Boolean isHidden);

    boolean existsByIdAndOwnerIdAndTrangThaiDuyetAndIsHidden(
            Integer id, Integer ownerId, String trangThaiDuyet, Boolean isHidden);
}
