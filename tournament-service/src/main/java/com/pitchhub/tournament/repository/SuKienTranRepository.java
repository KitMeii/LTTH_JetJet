package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.SuKienTran;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface SuKienTranRepository extends JpaRepository<SuKienTran, Integer> {

    List<SuKienTran> findByTranDauId(Integer tranDauId);

    List<SuKienTran> findByTranDauIdIn(List<Integer> tranDauIds);

    long countByTranDauIdAndLoaiSuKienAndDoiId(Integer tranDauId, String loaiSuKien, Integer doiId);

    long countByThanhVienIdAndLoaiSuKien(Integer thanhVienId, String loaiSuKien);
}
