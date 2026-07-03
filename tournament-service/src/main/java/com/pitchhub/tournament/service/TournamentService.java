package com.pitchhub.tournament.service;

import com.pitchhub.tournament.dto.request.TaoGiaiRequest;
import com.pitchhub.tournament.exception.TournamentException;
import com.pitchhub.tournament.model.*;
import com.pitchhub.tournament.repository.*;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.util.List;
import java.util.Set;

/**
 * Toan bo nghiep vu lifecycle giai dau. Controller chi goi vao day.
 * Port truc tiep tu TournamentService.cs + hoan thien xacNhanThanhToan
 * (truoc day chi nam trong comment "PATCH" chua bao gio duoc goi).
 */
@Service
public class TournamentService {

    private static final Set<Integer> SO_DOI_HOP_LE = Set.of(4, 8, 16, 32);

    private final GiaiDauRepository giaiDauRepository;
    private final BangDauRepository bangDauRepository;
    private final DoiBongRepository doiBongRepository;
    private final SanBongRepository sanBongRepository;
    private final TranDauRepository tranDauRepository;
    private final SuKienTranRepository suKienTranRepository;
    private final ScheduleService scheduleService;
    private final SuspensionService suspensionService;
    private final TournamentNotificationService notificationService;

    public TournamentService(GiaiDauRepository giaiDauRepository,
                              BangDauRepository bangDauRepository,
                              DoiBongRepository doiBongRepository,
                              SanBongRepository sanBongRepository,
                              TranDauRepository tranDauRepository,
                              SuKienTranRepository suKienTranRepository,
                              ScheduleService scheduleService,
                              SuspensionService suspensionService,
                              TournamentNotificationService notificationService) {
        this.giaiDauRepository = giaiDauRepository;
        this.bangDauRepository = bangDauRepository;
        this.doiBongRepository = doiBongRepository;
        this.sanBongRepository = sanBongRepository;
        this.tranDauRepository = tranDauRepository;
        this.suKienTranRepository = suKienTranRepository;
        this.scheduleService = scheduleService;
        this.suspensionService = suspensionService;
        this.notificationService = notificationService;
    }

    // ══════════════════════════════════════════════════════════
    // Tao giai dau + tu sinh cac bang A/B/C...
    // ══════════════════════════════════════════════════════════
    @Transactional
    public GiaiDau taoGiaiDau(TaoGiaiRequest dto, Integer ownerId) {
        boolean sanHopLe = sanBongRepository.existsByIdAndOwnerIdAndTrangThaiDuyetAndIsHidden(
                dto.getSanBongId(), ownerId, "DaDuyet", false);
        if (!sanHopLe) {
            throw new TournamentException(TournamentException.Code.KHONG_CO_QUYEN,
                    "San khong hop le hoac khong thuoc quyen quan ly cua ban!");
        }

        if (dto.getNgayBatDau().toLocalDate().isBefore(LocalDateTime.now().toLocalDate())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Ngay bat dau khong duoc la ngay da qua!");
        }
        if (!dto.getNgayKetThuc().isAfter(dto.getNgayBatDau())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Ngay ket thuc phai sau ngay bat dau!");
        }
        if (!SO_DOI_HOP_LE.contains(dto.getSoDoiToiDa())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "So doi toi da phai la 4, 8, 16 hoac 32!");
        }

        GiaiDau giai = new GiaiDau();
        giai.setTenGiai(dto.getTenGiai().trim());
        giai.setMoTa(dto.getMoTa() != null ? dto.getMoTa().trim() : null);
        giai.setSanBongId(dto.getSanBongId());
        giai.setOwnerId(ownerId);
        giai.setSoDoiToiDa(dto.getSoDoiToiDa());
        giai.setSoBang(dto.getSoBang());
        giai.setLePhiGiai(dto.getLePhiGiai());
        giai.setTienKyQuy(dto.getTienKyQuy());
        giai.setTienPhatTheVang(dto.getTienPhatTheVang() != null ? dto.getTienPhatTheVang() : BigDecimal.valueOf(20000));
        giai.setTienPhatTheDo(dto.getTienPhatTheDo() != null ? dto.getTienPhatTheDo() : BigDecimal.valueOf(100000));
        giai.setSoTranTreoGioTheDo(dto.getSoTranTreoGioTheDo() != null ? dto.getSoTranTreoGioTheDo() : 1);
        giai.setSoTheVangTichLuy(dto.getSoTheVangTichLuy() != null ? dto.getSoTheVangTichLuy() : 2);
        giai.setNgayBatDau(dto.getNgayBatDau());
        giai.setNgayKetThuc(dto.getNgayKetThuc());
        giai.setThoiGianDongDanhSach(dto.getThoiGianDong() != null ? dto.getThoiGianDong() : dto.getNgayBatDau().minusDays(1));
        giai.setTrangThai("Draft");
        giai.setThoiGianTao(LocalDateTime.now());

        giaiDauRepository.save(giai);

        for (int i = 0; i < dto.getSoBang(); i++) {
            BangDau bang = new BangDau();
            bang.setGiaiDau(giai);
            bang.setTenBang("Bang " + (char) ('A' + i));
            bangDauRepository.save(bang);
        }

        return giai;
    }

    // ══════════════════════════════════════════════════════════
    // Mo dang ky: Approved -> RegistrationOpen
    // ══════════════════════════════════════════════════════════
    @Transactional
    public void moiDangKy(Integer giaiId, Integer ownerId) {
        GiaiDau giai = layGiaiCuaOwner(giaiId, ownerId);

        if ("Draft".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Giai dau chua duoc Admin phe duyet, vui long cho Admin duyet truoc");
        }
        if (!"Approved".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Chi mo dang ky khi giai da duoc Admin phe duyet!");
        }

        giai.setTrangThai("RegistrationOpen");
        giaiDauRepository.save(giai);
    }

    // ══════════════════════════════════════════════════════════
    // Dong dang ky: RegistrationOpen -> RegistrationClosed
    // ══════════════════════════════════════════════════════════
    @Transactional
    public void dongDangKy(Integer giaiId, Integer ownerId) {
        GiaiDau giai = layGiaiCuaOwner(giaiId, ownerId);
        if (!"RegistrationOpen".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Chi dong dang ky khi giai dang mo!");
        }

        long soDoiHopLe = doiBongRepository.countByGiaiDauIdAndDaThanhToan(giaiId, true);
        if (soDoiHopLe < 2) {
            throw new TournamentException(TournamentException.Code.GIA_KHONG_DU_DOI,
                    "Can it nhat 2 doi da thanh toan de dong dang ky!");
        }

        giai.setTrangThai("RegistrationClosed");
        giai.setThoiGianDongDanhSach(LocalDateTime.now());
        giaiDauRepository.save(giai);
    }

    // ══════════════════════════════════════════════════════════
    // Gan doi vao bang (Drag & Drop)
    // ══════════════════════════════════════════════════════════
    @Transactional
    public void ganDoiVaoBang(Integer doiId, Integer bangId, Integer ownerId) {
        DoiBong doi = doiBongRepository.findById(doiId)
                .orElseThrow(() -> new TournamentException(
                        TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay doi!"));

        GiaiDau giai = layGiaiCuaOwner(doi.getGiaiDauId(), ownerId);
        if (!"RegistrationClosed".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Chi chia bang khi da dong dang ky!");
        }

        if (bangId != null) {
            BangDau bang = bangDauRepository.findById(bangId).orElse(null);
            if (bang == null || !bang.getGiaiDauId().equals(doi.getGiaiDauId())) {
                throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Bang khong hop le!");
            }
        }

        doi.setBangId(bangId);
        doiBongRepository.save(doi);
    }

    // ══════════════════════════════════════════════════════════
    // HOAN THIEN: Owner xac nhan da nhan tien chuyen khoan tay cua doi
    // (truoc day chi la comment "PATCH", chua bao gio active)
    // ══════════════════════════════════════════════════════════
    @Transactional
    public void xacNhanThanhToan(Integer giaiDauId, Integer doiId, Integer ownerId) {
        GiaiDau giai = layGiaiCuaOwner(giaiDauId, ownerId);

        DoiBong doi = doiBongRepository.findById(doiId)
                .orElseThrow(() -> new TournamentException(
                        TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay doi!"));
        if (!doi.getGiaiDauId().equals(giai.getId())) {
            throw new TournamentException(TournamentException.Code.KHONG_CO_QUYEN, "Doi khong thuoc giai nay!");
        }
        if (Boolean.TRUE.equals(doi.getDaThanhToan())) {
            throw new TournamentException(TournamentException.Code.DOI_DA_THANH_TOAN,
                    "Doi nay da duoc xac nhan roi!");
        }

        doi.setDaThanhToan(true);
        doi.setThoiGianThanhToan(LocalDateTime.now());
        doiBongRepository.save(doi);

        notificationService.guiEmailXacNhanThanhToan(doiId);
    }

    // ══════════════════════════════════════════════════════════
    // Khoi tao giai: sinh lich (moi bang) + chuyen Active
    // RegistrationClosed -> Active
    // ══════════════════════════════════════════════════════════
    @Transactional
    public void khoiTaoGiai(Integer giaiId, Integer ownerId) {
        GiaiDau giai = layGiaiCuaOwner(giaiId, ownerId);
        if (!"RegistrationClosed".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Chi khoi tao sau khi dong dang ky!");
        }

        List<DoiBong> doiBongs = doiBongRepository.findByGiaiDauId(giaiId);
        long doiChuaBang = doiBongs.stream()
                .filter(d -> d.getBangId() == null && Boolean.TRUE.equals(d.getDaThanhToan())).count();
        if (doiChuaBang > 0) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Con " + doiChuaBang + " doi chua duoc xep bang!");
        }

        List<BangDau> bangDaus = bangDauRepository.findByGiaiDauId(giaiId);
        for (BangDau bang : bangDaus) {
            List<DoiBong> doiTrongBang = doiBongs.stream()
                    .filter(d -> bang.getId().equals(d.getBangId()) && Boolean.TRUE.equals(d.getDaThanhToan()))
                    .toList();
            if (doiTrongBang.size() < 2) continue;

            List<TranDau> lichBang = scheduleService.sinhLichVongTron(
                    giaiId, bang.getId(), doiTrongBang, giai.getNgayBatDau());
            tranDauRepository.saveAll(lichBang);
        }

        giai.setTrangThai("Active");
        giaiDauRepository.save(giai);

        notificationService.guiEmailLichDau(giaiId);
    }

    // ══════════════════════════════════════════════════════════
    // Ket thuc giai: Active -> Finished
    // ══════════════════════════════════════════════════════════
    @Transactional
    public void ketThucGiai(Integer giaiId, Integer ownerId) {
        GiaiDau giai = layGiaiCuaOwner(giaiId, ownerId);
        if (!"Active".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Chi ket thuc khi giai dang Active!");
        }

        long tranChuaXong = tranDauRepository.countByGiaiDauIdAndTrangThaiIn(
                giaiId, List.of("Scheduled", "InProgress"));
        if (tranChuaXong > 0) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Con " + tranChuaXong + " tran chua ket thuc!");
        }

        giai.setTrangThai("Finished");
        giaiDauRepository.save(giai);
    }

    // ══════════════════════════════════════════════════════════
    // Xu ly su co: doi bo cuoc -> xu thua 0-3, mat 100% ky quy
    // ══════════════════════════════════════════════════════════
    @Transactional
    public void xuLySuCo(Integer tranDauId, Integer doiBoCuocId, String lyDo, Integer ownerId) {
        TranDau tran = tranDauRepository.findById(tranDauId)
                .orElseThrow(() -> new TournamentException(
                        TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay tran!"));

        layGiaiCuaOwner(tran.getGiaiDauId(), ownerId);

        if ("Closed".equals(tran.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Tran da ket thuc!");
        }
        if (!doiBoCuocId.equals(tran.getDoiNhaId()) && !doiBoCuocId.equals(tran.getDoiKhachId())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Doi khong tham gia tran nay!");
        }
        if (lyDo == null || lyDo.isBlank()) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Phai nhap ly do xu ly su co!");
        }

        boolean doiNhaBoCuoc = doiBoCuocId.equals(tran.getDoiNhaId());
        tran.setBanThangNha(doiNhaBoCuoc ? 0 : 3);
        tran.setBanThangKhach(doiNhaBoCuoc ? 3 : 0);
        tran.setTrangThai("Closed");
        tranDauRepository.save(tran);

        SuKienTran suCo = new SuKienTran();
        suCo.setTranDau(tran);
        DoiBong doiBoCuoc = new DoiBong();
        doiBoCuoc.setId(doiBoCuocId);
        suCo.setDoi(doiBoCuoc);
        suCo.setLoaiSuKien("SuCo");
        suCo.setGhiChu("Bo cuoc. Ly do: " + lyDo);
        suCo.setThoiGianGhi(LocalDateTime.now());
        suKienTranRepository.save(suCo);

        doiBongRepository.findById(doiBoCuocId).ifPresent(doi -> {
            doi.setTienKyQuyConLai(BigDecimal.ZERO);
            doiBongRepository.save(doi);
        });

        suspensionService.xuLyTreoGio(tran.getGiaiDauId());
    }

    // ── Helper ─────────────────────────────────────────────
    private GiaiDau layGiaiCuaOwner(Integer giaiId, Integer ownerId) {
        GiaiDau giai = giaiDauRepository.findById(giaiId)
                .orElseThrow(() -> new TournamentException(
                        TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!"));
        if (!giai.getOwnerId().equals(ownerId)) {
            throw new TournamentException(TournamentException.Code.KHONG_CO_QUYEN, "Ban khong quan ly giai nay!");
        }
        return giai;
    }
}
