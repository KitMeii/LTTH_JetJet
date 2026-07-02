package com.pitchhub.tournament.service;

import com.pitchhub.tournament.model.GiaiDau;
import com.pitchhub.tournament.model.SuKienTran;
import com.pitchhub.tournament.model.ThanhVienDoi;
import com.pitchhub.tournament.repository.GiaiDauRepository;
import com.pitchhub.tournament.repository.SuKienTranRepository;
import com.pitchhub.tournament.repository.ThanhVienDoiRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

/**
 * Xu ly treo gio cau thu sau moi tran.
 * Logic: The do -> treo N tran / Tich luy the vang -> treo 1 tran.
 * Port truc tiep tu SuspensionService.cs — goi dong bo sau khi tran Closed
 * (KHONG dung background job dinh ky, da bo TournamentBackgroundService).
 */
@Service
public class SuspensionService {

    private final GiaiDauRepository giaiDauRepository;
    private final SuKienTranRepository suKienTranRepository;
    private final ThanhVienDoiRepository thanhVienDoiRepository;

    public SuspensionService(GiaiDauRepository giaiDauRepository,
                              SuKienTranRepository suKienTranRepository,
                              ThanhVienDoiRepository thanhVienDoiRepository) {
        this.giaiDauRepository = giaiDauRepository;
        this.suKienTranRepository = suKienTranRepository;
        this.thanhVienDoiRepository = thanhVienDoiRepository;
    }

    @Transactional
    public void xuLyTreoGio(Integer giaiDauId) {
        GiaiDau giai = giaiDauRepository.findDetailById(giaiDauId).orElse(null);
        if (giai == null) return;

        List<Integer> tranDauIds = giai.getTranDaus().stream().map(t -> t.getId()).toList();
        List<SuKienTran> suKiens = suKienTranRepository.findByTranDauIdIn(tranDauIds).stream()
                .filter(s -> s.getThanhVienId() != null
                        && ("TheVang".equals(s.getLoaiSuKien())
                        || "TheVangLan2".equals(s.getLoaiSuKien())
                        || "TheDo".equals(s.getLoaiSuKien())))
                .toList();

        for (var doi : giai.getDoiBongs()) {
            for (ThanhVienDoi tv : doi.getThanhViens()) {
                List<SuKienTran> suKienCuaTv = suKiens.stream()
                        .filter(s -> tv.getId().equals(s.getThanhVienId()))
                        .toList();

                long soTheDo = suKienCuaTv.stream().filter(s -> "TheDo".equals(s.getLoaiSuKien())).count();
                long soTheVang = suKienCuaTv.stream()
                        .filter(s -> "TheVang".equals(s.getLoaiSuKien()) || "TheVangLan2".equals(s.getLoaiSuKien()))
                        .count();
                long treoDoVang = soTheVang / giai.getSoTheVangTichLuy();

                int soTranTreo = (int) (soTheDo * giai.getSoTranTreoGioTheDo() + treoDoVang);

                if (!tv.getSoTranTreoGio().equals(soTranTreo)) {
                    tv.setSoTranTreoGio(soTranTreo);
                    thanhVienDoiRepository.save(tv);
                }
            }
        }
    }

    public boolean biTreoGio(ThanhVienDoi thanhVien, int soTranDaThiDau) {
        return thanhVien.getSoTranTreoGio() > soTranDaThiDau;
    }

    public List<ThanhVienDoi> getDanhSachTreoGio(Integer giaiDauId, int vongKeTiep) {
        GiaiDau giai = giaiDauRepository.findDetailById(giaiDauId).orElse(null);
        if (giai == null) return List.of();

        return giai.getDoiBongs().stream()
                .flatMap(d -> d.getThanhViens().stream())
                .filter(tv -> tv.getSoTranTreoGio() >= vongKeTiep)
                .toList();
    }
}
