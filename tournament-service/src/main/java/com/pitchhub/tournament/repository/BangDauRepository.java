package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.BangDau;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface BangDauRepository extends JpaRepository<BangDau, Integer> {
    List<BangDau> findByGiaiDauId(Integer giaiDauId);
}
