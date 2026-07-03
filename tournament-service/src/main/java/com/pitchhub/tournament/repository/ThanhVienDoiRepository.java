package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.ThanhVienDoi;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface ThanhVienDoiRepository extends JpaRepository<ThanhVienDoi, Integer> {

    List<ThanhVienDoi> findByDoiId(Integer doiId);

    boolean existsByDoiIdAndSoAo(Integer doiId, Integer soAo);
}
