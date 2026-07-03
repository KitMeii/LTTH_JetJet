package com.pitchhub.tournament.service;

import com.pitchhub.tournament.dto.response.StandingRowResponse;
import com.pitchhub.tournament.model.BangDau;
import com.pitchhub.tournament.model.DoiBong;
import com.pitchhub.tournament.model.GiaiDau;
import com.pitchhub.tournament.model.TranDau;
import com.pitchhub.tournament.repository.GiaiDauRepository;
import org.springframework.stereotype.Service;

import java.util.*;
import java.util.stream.Collectors;

/**
 * Tinh bang xep hang theo chuan FIFA tie-breakers.
 * Port truc tiep tu StandingService.cs — khong luu vao DB, tinh dong moi lan goi.
 */
@Service
public class StandingService {

    private final GiaiDauRepository giaiDauRepository;

    public StandingService(GiaiDauRepository giaiDauRepository) {
        this.giaiDauRepository = giaiDauRepository;
    }

    public Map<Integer, List<StandingRowResponse>> getStandings(Integer giaiDauId) {
        GiaiDau giai = giaiDauRepository.findDetailById(giaiDauId).orElse(null);
        if (giai == null) return new LinkedHashMap<>();

        Map<Integer, List<StandingRowResponse>> result = new LinkedHashMap<>();

        for (BangDau bang : giai.getBangDaus()) {
            List<DoiBong> doiTrongBang = giai.getDoiBongs().stream()
                    .filter(d -> bang.getId().equals(d.getBangId()))
                    .toList();

            Map<Integer, StandingRowResponse> stats = new LinkedHashMap<>();
            for (DoiBong d : doiTrongBang) {
                StandingRowResponse row = new StandingRowResponse();
                row.setDoiId(d.getId());
                row.setTenDoi(d.getTenDoi());
                row.setLogoUrl(d.getLogoUrl());
                stats.put(d.getId(), row);
            }

            List<TranDau> transClosed = giai.getTranDaus().stream()
                    .filter(t -> bang.getId().equals(t.getBangId()) && "Closed".equals(t.getTrangThai()))
                    .toList();

            for (TranDau tran : transClosed) {
                if (tran.getBanThangNha() == null || tran.getBanThangKhach() == null) continue;
                if (tran.getDoiNhaId() == null || tran.getDoiKhachId() == null) continue;
                if (!stats.containsKey(tran.getDoiNhaId()) || !stats.containsKey(tran.getDoiKhachId())) continue;

                StandingRowResponse nha = stats.get(tran.getDoiNhaId());
                StandingRowResponse khach = stats.get(tran.getDoiKhachId());

                nha.setSoTran(nha.getSoTran() + 1);
                khach.setSoTran(khach.getSoTran() + 1);
                nha.setBanThang(nha.getBanThang() + tran.getBanThangNha());
                nha.setBanThua(nha.getBanThua() + tran.getBanThangKhach());
                khach.setBanThang(khach.getBanThang() + tran.getBanThangKhach());
                khach.setBanThua(khach.getBanThua() + tran.getBanThangNha());

                if (tran.getBanThangNha() > tran.getBanThangKhach()) {
                    nha.setThang(nha.getThang() + 1);
                    nha.setDiem(nha.getDiem() + 3);
                    khach.setThua(khach.getThua() + 1);
                } else if (tran.getBanThangNha() < tran.getBanThangKhach()) {
                    khach.setThang(khach.getThang() + 1);
                    khach.setDiem(khach.getDiem() + 3);
                    nha.setThua(nha.getThua() + 1);
                } else {
                    nha.setHoa(nha.getHoa() + 1);
                    nha.setDiem(nha.getDiem() + 1);
                    khach.setHoa(khach.getHoa() + 1);
                    khach.setDiem(khach.getDiem() + 1);
                }
            }

            List<StandingRowResponse> sorted = sapXepFifa(new ArrayList<>(stats.values()), transClosed);
            for (int i = 0; i < sorted.size(); i++) {
                sorted.get(i).setThuHang(i + 1);
            }

            result.put(bang.getId(), sorted);
        }

        return result;
    }

    /** FIFA tie-breakers: Diem -> Doi dau truc tiep -> Hieu so -> Tong ban thang. */
    private List<StandingRowResponse> sapXepFifa(List<StandingRowResponse> rows, List<TranDau> tatCaTran) {
        Map<Integer, List<StandingRowResponse>> groups = rows.stream()
                .collect(Collectors.groupingBy(StandingRowResponse::getDiem, LinkedHashMap::new, Collectors.toList()));

        List<Integer> diemGiam = new ArrayList<>(groups.keySet());
        diemGiam.sort(Comparator.reverseOrder());

        List<StandingRowResponse> result = new ArrayList<>();

        for (Integer diem : diemGiam) {
            List<StandingRowResponse> doiCungDiem = groups.get(diem);

            if (doiCungDiem.size() == 1) {
                result.addAll(doiCungDiem);
                continue;
            }

            Set<Integer> doiIds = doiCungDiem.stream().map(StandingRowResponse::getDoiId).collect(Collectors.toSet());
            List<TranDau> tranDoiDau = tatCaTran.stream()
                    .filter(t -> t.getDoiNhaId() != null && t.getDoiKhachId() != null
                            && doiIds.contains(t.getDoiNhaId()) && doiIds.contains(t.getDoiKhachId()))
                    .toList();

            Map<Integer, int[]> ddStats = new HashMap<>(); // [diem, banThang, banThua]
            for (StandingRowResponse d : doiCungDiem) ddStats.put(d.getDoiId(), new int[]{0, 0, 0});

            for (TranDau t : tranDoiDau) {
                if (t.getBanThangNha() == null || t.getDoiNhaId() == null || t.getDoiKhachId() == null) continue;
                int[] nha = ddStats.get(t.getDoiNhaId());
                int[] khach = ddStats.get(t.getDoiKhachId());

                nha[1] += t.getBanThangNha();
                nha[2] += t.getBanThangKhach();
                khach[1] += t.getBanThangKhach();
                khach[2] += t.getBanThangNha();

                if (t.getBanThangNha() > t.getBanThangKhach()) {
                    nha[0] += 3;
                } else if (t.getBanThangNha() < t.getBanThangKhach()) {
                    khach[0] += 3;
                } else {
                    nha[0] += 1;
                    khach[0] += 1;
                }
            }

            List<StandingRowResponse> sorted = doiCungDiem.stream()
                    .sorted(Comparator
                            .comparingInt((StandingRowResponse d) -> ddStats.get(d.getDoiId())[0]).reversed()
                            .thenComparing(Comparator.comparingInt((StandingRowResponse d) ->
                                    ddStats.get(d.getDoiId())[1] - ddStats.get(d.getDoiId())[2]).reversed())
                            .thenComparing(Comparator.comparingInt((StandingRowResponse d) ->
                                    ddStats.get(d.getDoiId())[1]).reversed())
                            .thenComparing(Comparator.comparingInt(StandingRowResponse::getHieuSo).reversed())
                            .thenComparing(Comparator.comparingInt(StandingRowResponse::getBanThang).reversed()))
                    .toList();

            result.addAll(sorted);
        }

        return result;
    }
}
