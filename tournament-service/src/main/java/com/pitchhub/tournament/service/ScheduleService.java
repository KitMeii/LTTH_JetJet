package com.pitchhub.tournament.service;

import com.pitchhub.tournament.model.DoiBong;
import com.pitchhub.tournament.model.TranDau;
import org.springframework.stereotype.Service;

import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.List;

/**
 * Sinh lich thi dau theo thuat toan Berger (Round Robin).
 * Port truc tiep tu ScheduleService.cs.
 */
@Service
public class ScheduleService {

    /** Sinh lich vong tron cho 1 bang — goi lap lai cho tung bang trong TournamentService. */
    public List<TranDau> sinhLichVongTron(Integer giaiDauId, Integer bangId,
                                           List<DoiBong> danhSachDoi, LocalDateTime ngayDau) {
        if (danhSachDoi.size() < 2) return new ArrayList<>();
        return sinhLichBerger(danhSachDoi, bangId, giaiDauId, ngayDau);
    }

    /** Berger Algorithm — co dinh phan tu dau, xoay vong cac phan tu con lai. */
    private List<TranDau> sinhLichBerger(List<DoiBong> dsDoiGoc, Integer bangId,
                                          Integer giaiDauId, LocalDateTime ngayDau) {
        List<TranDau> result = new ArrayList<>();

        List<DoiBong> ds = new ArrayList<>(dsDoiGoc);
        boolean coBye = ds.size() % 2 != 0;
        if (coBye) ds.add(null); // BYE

        int n = ds.size();
        int soVong = n - 1;
        int soTranMoiVong = n / 2;

        for (int vong = 0; vong < soVong; vong++) {
            LocalDateTime ngayVong = ngayDau.plusDays((long) vong * 7); // moi vong cach 1 tuan

            for (int slot = 0; slot < soTranMoiVong; slot++) {
                DoiBong doiNha = ds.get(slot);
                DoiBong doiKhach = ds.get(n - 1 - slot);

                if (doiNha == null || doiKhach == null) continue;

                boolean doiNhaThuat = (vong + slot) % 2 == 0;

                TranDau tran = new TranDau();
                tran.setGiaiDauId(giaiDauId);
                tran.setBangId(bangId);
                tran.setDoiNha(doiNhaThuat ? doiNha : doiKhach);
                tran.setDoiKhach(doiNhaThuat ? doiKhach : doiNha);
                tran.setVongDau(vong + 1);
                tran.setLoaiVong("VongBang");
                tran.setNgayThiDau(ngayVong);
                tran.setTrangThai("Scheduled");
                result.add(tran);
            }

            // Xoay Berger: co dinh ds[0], xoay ds[1..n-1]
            DoiBong last = ds.get(n - 1);
            for (int i = n - 1; i > 1; i--) {
                ds.set(i, ds.get(i - 1));
            }
            ds.set(1, last);
        }

        return result;
    }
}
