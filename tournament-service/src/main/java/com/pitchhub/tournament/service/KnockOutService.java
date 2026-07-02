package com.pitchhub.tournament.service;

import com.pitchhub.tournament.dto.response.BracketResponse;
import com.pitchhub.tournament.dto.response.StandingRowResponse;
import com.pitchhub.tournament.dto.response.TranDauResponse;
import com.pitchhub.tournament.exception.TournamentException;
import com.pitchhub.tournament.model.BangDau;
import com.pitchhub.tournament.model.DoiBong;
import com.pitchhub.tournament.model.GiaiDau;
import com.pitchhub.tournament.model.TranDau;
import com.pitchhub.tournament.repository.GiaiDauRepository;
import com.pitchhub.tournament.repository.TranDauRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.*;
import java.util.stream.Collectors;

/**
 * Sinh lich vong Knock-out (Tu ket -> Ban ket -> Chung ket) dua tren ket
 * qua vong bang — lay nhat/nhi moi bang.
 *
 * TINH NANG HOAN THIEN: ben C# service nay da viet xong nhung KHONG duoc
 * dang ky DI, KHONG duoc TournamentService/Controller nao goi toi (dead
 * code). O day noi day day du: TournamentController goi sinhVongKnockOut()
 * sau khi vong bang xong, va TournamentStaffController goi
 * capNhatDoiKnockOut() moi khi mot tran knock-out duoc chot ket qua.
 */
@Service
public class KnockOutService {

    private final GiaiDauRepository giaiDauRepository;
    private final TranDauRepository tranDauRepository;
    private final StandingService standingService;

    public KnockOutService(GiaiDauRepository giaiDauRepository,
                            TranDauRepository tranDauRepository,
                            StandingService standingService) {
        this.giaiDauRepository = giaiDauRepository;
        this.tranDauRepository = tranDauRepository;
        this.standingService = standingService;
    }

    @Transactional
    public void sinhVongKnockOut(Integer giaiDauId) {
        GiaiDau giai = giaiDauRepository.findDetailById(giaiDauId)
                .orElseThrow(() -> new TournamentException(
                        TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!"));

        boolean daCoKO = giai.getTranDaus().stream().anyMatch(t -> !"VongBang".equals(t.getLoaiVong()));
        if (daCoKO) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Vong knock-out da duoc sinh roi!");
        }

        Map<Integer, List<StandingRowResponse>> bxh = standingService.getStandings(giaiDauId);
        List<DoiVao> doiVao = new ArrayList<>();

        for (BangDau bang : giai.getBangDaus()) {
            List<StandingRowResponse> rows = bxh.get(bang.getId());
            if (rows == null || rows.size() < 2) {
                throw new TournamentException(TournamentException.Code.GIA_KHONG_DU_DOI,
                        bang.getTenBang() + " chua du ket qua!");
            }

            DoiBong nhat = timDoi(giai.getDoiBongs(), rows.get(0).getDoiId());
            DoiBong nhi = timDoi(giai.getDoiBongs(), rows.get(1).getDoiId());
            if (nhat == null || nhi == null) {
                throw new TournamentException(TournamentException.Code.GIA_KHONG_DU_DOI,
                        "Khong xac dinh duoc doi tu " + bang.getTenBang() + "!");
            }

            doiVao.add(new DoiVao(nhat, 1, bang.getTenBang()));
            doiVao.add(new DoiVao(nhi, 2, bang.getTenBang()));
        }

        int soDoiKO = doiVao.size();
        List<TranDau> tranKO = new ArrayList<>();
        LocalDateTime ngayKO = giai.getNgayKetThuc().minusDays(21);

        if (soDoiKO == 8) {
            tranKO.addAll(sinhTuKet8Doi(doiVao, giaiDauId, ngayKO));
            tranKO.addAll(sinhBanKetPlaceholder(giaiDauId, ngayKO.plusDays(7)));
            tranKO.add(sinhChungKetPlaceholder(giaiDauId, ngayKO.plusDays(14)));
        } else if (soDoiKO == 4) {
            tranKO.addAll(sinhBanKet4Doi(doiVao, giaiDauId, ngayKO));
            tranKO.add(sinhChungKetPlaceholder(giaiDauId, ngayKO.plusDays(7)));
        } else if (soDoiKO == 2) {
            TranDau chungKet = new TranDau();
            chungKet.setGiaiDauId(giaiDauId);
            chungKet.setDoiNha(doiVao.get(0).doi);
            chungKet.setDoiKhach(doiVao.get(1).doi);
            chungKet.setVongDau(99);
            chungKet.setLoaiVong("ChungKet");
            chungKet.setNgayThiDau(ngayKO);
            chungKet.setTrangThai("Scheduled");
            tranKO.add(chungKet);
        } else {
            throw new TournamentException(TournamentException.Code.GIA_KHONG_DU_DOI,
                    "So doi vao knock-out (" + soDoiKO + ") khong hop le! Can 2, 4 hoac 8 doi.");
        }

        tranDauRepository.saveAll(tranKO);
    }

    /** Goi sau khi mot tran TrangThai -> Closed — dien doi thang vao tran ke tiep. */
    @Transactional
    public void capNhatDoiKnockOut(Integer tranDauId) {
        TranDau tran = tranDauRepository.findById(tranDauId).orElse(null);
        if (tran == null || !"Closed".equals(tran.getTrangThai())) return;
        if (tran.getBanThangNha() == null) return;

        Integer doiThang = tran.getBanThangNha() > tran.getBanThangKhach() ? tran.getDoiNhaId() : tran.getDoiKhachId();
        if (doiThang == null) return;

        String vongTiepTheo = switch (tran.getLoaiVong()) {
            case "TuKet" -> "BanKet";
            case "BanKet" -> "ChungKet";
            default -> null;
        };
        if (vongTiepTheo == null) return;

        List<TranDau> ungVien = tranDauRepository.findByGiaiDauIdAndLoaiVong(tran.getGiaiDauId(), vongTiepTheo)
                .stream()
                .filter(t -> "Pending".equals(t.getTrangThai()))
                .sorted(Comparator.comparing(TranDau::getId))
                .toList();

        if (ungVien.isEmpty()) return;
        TranDau tranPending = ungVien.get(0);

        DoiBong doi = new DoiBong();
        doi.setId(doiThang);

        // "0" la sentinel TBD (DB NOT NULL DEFAULT 0, xem @DynamicInsert tren
        // TranDau) — coi 0 giong null khi kiem tra slot con trong.
        if (tranPending.getDoiNhaId() == null || tranPending.getDoiNhaId() == 0) {
            tranPending.setDoiNha(doi);
        } else if (tranPending.getDoiKhachId() == null || tranPending.getDoiKhachId() == 0) {
            tranPending.setDoiKhach(doi);
            tranPending.setTrangThai("Scheduled");
        }
        tranDauRepository.save(tranPending);
    }

    public BracketResponse getBracket(Integer giaiDauId) {
        List<TranDau> all = tranDauRepository.findByGiaiDauId(giaiDauId).stream()
                .filter(t -> !"VongBang".equals(t.getLoaiVong()))
                .sorted(Comparator.comparing(TranDau::getVongDau))
                .toList();

        Map<String, List<TranDau>> byRound = all.stream()
                .collect(Collectors.groupingBy(TranDau::getLoaiVong, LinkedHashMap::new, Collectors.toList()));

        List<BracketResponse.Round> rounds = new ArrayList<>();
        for (var e : byRound.entrySet()) {
            rounds.add(new BracketResponse.Round(e.getKey(),
                    e.getValue().stream().map(TranDauResponse::from).toList()));
        }

        return new BracketResponse(giaiDauId, rounds);
    }

    // ── 8 doi -> 4 tran Tu ket. Seeding: A1 vs D2, B1 vs C2, C1 vs B2, D1 vs A2 ──
    private List<TranDau> sinhTuKet8Doi(List<DoiVao> doiVao, Integer giaiDauId, LocalDateTime ngay) {
        List<List<DoiVao>> bangMap = groupByBang(doiVao);
        List<TranDau> result = new ArrayList<>();
        int n = bangMap.size();

        for (int i = 0; i < n / 2; i++) {
            List<DoiVao> bangNhat = bangMap.get(i);
            List<DoiVao> bangNhi = bangMap.get(n - 1 - i);

            DoiBong nhat1 = timTheoHang(bangNhat, 1);
            DoiBong nhi2 = timTheoHang(bangNhi, 2);
            DoiBong nhat2 = timTheoHang(bangNhi, 1);
            DoiBong nhi1 = timTheoHang(bangNhat, 2);

            result.add(taoTran(giaiDauId, nhat1, nhi2, 10, "TuKet", ngay));
            result.add(taoTran(giaiDauId, nhat2, nhi1, 10, "TuKet", ngay));
        }
        return result;
    }

    // ── 4 doi -> 2 tran Ban ket ──
    private List<TranDau> sinhBanKet4Doi(List<DoiVao> doiVao, Integer giaiDauId, LocalDateTime ngay) {
        List<List<DoiVao>> bangMap = groupByBang(doiVao);

        DoiBong nhat1 = timTheoHang(bangMap.get(0), 1);
        DoiBong nhi2 = timTheoHang(bangMap.get(1), 2);
        DoiBong nhat2 = timTheoHang(bangMap.get(1), 1);
        DoiBong nhi1 = timTheoHang(bangMap.get(0), 2);

        return List.of(
                taoTran(giaiDauId, nhat1, nhi2, 20, "BanKet", ngay),
                taoTran(giaiDauId, nhat2, nhi1, 20, "BanKet", ngay)
        );
    }

    // ── Placeholder Ban ket (cho ket qua Tu ket) ──
    private List<TranDau> sinhBanKetPlaceholder(Integer giaiDauId, LocalDateTime ngay) {
        return List.of(
                taoTranPending(giaiDauId, 20, "BanKet", ngay),
                taoTranPending(giaiDauId, 20, "BanKet", ngay)
        );
    }

    private TranDau sinhChungKetPlaceholder(Integer giaiDauId, LocalDateTime ngay) {
        return taoTranPending(giaiDauId, 30, "ChungKet", ngay);
    }

    private TranDau taoTran(Integer giaiDauId, DoiBong doiNha, DoiBong doiKhach,
                             int vongDau, String loaiVong, LocalDateTime ngay) {
        TranDau t = new TranDau();
        t.setGiaiDauId(giaiDauId);
        t.setDoiNha(doiNha);
        t.setDoiKhach(doiKhach);
        t.setVongDau(vongDau);
        t.setLoaiVong(loaiVong);
        t.setNgayThiDau(ngay);
        t.setTrangThai("Scheduled");
        return t;
    }

    private TranDau taoTranPending(Integer giaiDauId, int vongDau, String loaiVong, LocalDateTime ngay) {
        TranDau t = new TranDau();
        t.setGiaiDauId(giaiDauId);
        t.setVongDau(vongDau);
        t.setLoaiVong(loaiVong);
        t.setNgayThiDau(ngay);
        t.setTrangThai("Pending");
        return t;
    }

    private List<List<DoiVao>> groupByBang(List<DoiVao> doiVao) {
        Map<String, List<DoiVao>> grouped = doiVao.stream()
                .collect(Collectors.groupingBy(DoiVao::bang, LinkedHashMap::new, Collectors.toList()));
        List<String> keys = new ArrayList<>(grouped.keySet());
        Collections.sort(keys);
        List<List<DoiVao>> result = new ArrayList<>();
        for (String k : keys) result.add(grouped.get(k));
        return result;
    }

    private DoiBong timTheoHang(List<DoiVao> nhom, int thuHang) {
        return nhom.stream().filter(d -> d.thuHang() == thuHang).findFirst()
                .map(DoiVao::doi).orElse(null);
    }

    private DoiBong timDoi(Collection<DoiBong> doiBongs, Integer id) {
        return doiBongs.stream().filter(d -> d.getId().equals(id)).findFirst().orElse(null);
    }

    private record DoiVao(DoiBong doi, int thuHang, String bang) {
    }
}
