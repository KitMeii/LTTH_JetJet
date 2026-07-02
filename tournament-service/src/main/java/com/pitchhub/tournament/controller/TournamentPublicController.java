package com.pitchhub.tournament.controller;

import com.pitchhub.tournament.dto.request.DangKyDoiRequest;
import com.pitchhub.tournament.dto.request.ThemThanhVienRequest;
import com.pitchhub.tournament.dto.response.*;
import com.pitchhub.tournament.exception.TournamentException;
import com.pitchhub.tournament.model.*;
import com.pitchhub.tournament.repository.*;
import com.pitchhub.tournament.security.AuthenticatedUser;
import com.pitchhub.tournament.service.KnockOutService;
import com.pitchhub.tournament.service.StandingService;
import com.pitchhub.tournament.service.TournamentNotificationService;
import jakarta.validation.Valid;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.util.Comparator;
import java.util.List;
import java.util.Map;

/**
 * /api/public/tournament — tim kiem/xem giai dau cong khai, dang ky doi.
 * Tuong duong TournamentPublicController.cs. Chi cac endpoint dang ky/roster
 * (POST/DELETE) yeu cau dang nhap (bat ky role nao) — con lai la public.
 *
 * GHI CHU: Cac endpoint dang ky/them-xoa-cau-thu (register, teams/**) KHONG
 * nam trong danh sach 6 endpoint liet ke ban dau — them vao de dam bao du
 * ~33 action goc co endpoint tuong ung (khong co cach nao tao DoiBong qua
 * REST API neu thieu chung).
 */
@RestController
@RequestMapping("/public/tournament")
public class TournamentPublicController {

    private final GiaiDauRepository giaiDauRepository;
    private final DoiBongRepository doiBongRepository;
    private final ThanhVienDoiRepository thanhVienDoiRepository;
    private final TranDauRepository tranDauRepository;
    private final SanBongRepository sanBongRepository;
    private final UserRepository userRepository;
    private final StandingService standingService;
    private final KnockOutService knockOutService;
    private final TournamentNotificationService notificationService;

    public TournamentPublicController(GiaiDauRepository giaiDauRepository,
                                       DoiBongRepository doiBongRepository,
                                       ThanhVienDoiRepository thanhVienDoiRepository,
                                       TranDauRepository tranDauRepository,
                                       SanBongRepository sanBongRepository,
                                       UserRepository userRepository,
                                       StandingService standingService,
                                       KnockOutService knockOutService,
                                       TournamentNotificationService notificationService) {
        this.giaiDauRepository = giaiDauRepository;
        this.doiBongRepository = doiBongRepository;
        this.thanhVienDoiRepository = thanhVienDoiRepository;
        this.tranDauRepository = tranDauRepository;
        this.sanBongRepository = sanBongRepository;
        this.userRepository = userRepository;
        this.standingService = standingService;
        this.knockOutService = knockOutService;
        this.notificationService = notificationService;
    }

    // GET /public/tournament — tim kiem giai cong khai
    @GetMapping
    public ApiResponse<List<GiaiDauResponse>> index(
            @RequestParam(required = false) String keyword,
            @RequestParam(required = false) String quan,
            @RequestParam(required = false) String trangThai,
            @RequestParam(required = false) BigDecimal lePhiTu,
            @RequestParam(required = false) BigDecimal lePhiDen,
            @RequestParam(required = false) String sapXep) {

        List<GiaiDau> all = giaiDauRepository.findAll().stream()
                .filter(g -> !"Draft".equals(g.getTrangThai()))
                .toList();

        List<SanBong> sanBongs = sanBongRepository.findAll();

        var stream = all.stream();
        if (keyword != null && !keyword.isBlank()) {
            String kw = keyword.toLowerCase();
            stream = stream.filter(g -> g.getTenGiai().toLowerCase().contains(kw)
                    || (g.getMoTa() != null && g.getMoTa().toLowerCase().contains(kw)));
        }
        if (quan != null && !quan.isBlank()) {
            stream = stream.filter(g -> sanBongs.stream()
                    .anyMatch(s -> s.getId().equals(g.getSanBongId()) && quan.equals(s.getQuan())));
        }
        if (trangThai != null && !trangThai.isBlank()) {
            stream = stream.filter(g -> trangThai.equals(g.getTrangThai()));
        }
        if (lePhiTu != null) stream = stream.filter(g -> g.getLePhiGiai().compareTo(lePhiTu) >= 0);
        if (lePhiDen != null) stream = stream.filter(g -> g.getLePhiGiai().compareTo(lePhiDen) <= 0);

        List<GiaiDau> filtered = stream.toList();

        Comparator<GiaiDau> comparator = switch (sapXep == null ? "" : sapXep) {
            case "lephi_tang" -> Comparator.comparing(GiaiDau::getLePhiGiai);
            case "lephi_giam" -> Comparator.comparing(GiaiDau::getLePhiGiai).reversed();
            case "ngay_gan" -> Comparator.comparing(GiaiDau::getNgayBatDau);
            case "moi_nhat" -> Comparator.comparing(GiaiDau::getThoiGianTao).reversed();
            default -> Comparator.comparing((GiaiDau g) -> "RegistrationOpen".equals(g.getTrangThai()) ? 0 : 1)
                    .thenComparing(Comparator.comparing(GiaiDau::getNgayBatDau).reversed());
        };

        List<GiaiDauResponse> result = filtered.stream().sorted(comparator)
                .map(g -> enrich(GiaiDauResponse.from(g), g)).toList();

        return ApiResponse.ok(result);
    }

    // GET /public/tournament/{id} — chi tiet giai cong khai
    @GetMapping("/{id}")
    public ApiResponse<GiaiDauResponse> details(@PathVariable Integer id) {
        GiaiDau giai = timGiaiCongKhai(id);
        GiaiDauResponse response = enrich(GiaiDauResponse.from(giai), giai);
        response.setDoiBongs(giai.getDoiBongs().stream().map(DoiBongResponse::from).toList());
        response.setTranDaus(giai.getTranDaus().stream().map(TranDauResponse::from).toList());
        response.setBangDaus(giai.getBangDaus().stream()
                .map(b -> new GiaiDauResponse.BangDauSummary(b.getId(), b.getTenBang())).toList());
        return ApiResponse.ok(response);
    }

    // GET /public/tournament/{id}/standings — BXH tat ca bang
    @GetMapping("/{id}/standings")
    public ApiResponse<Map<Integer, List<StandingRowResponse>>> standings(@PathVariable Integer id) {
        timGiaiCongKhai(id);
        return ApiResponse.ok(standingService.getStandings(id));
    }

    // GET /public/tournament/{id}/bracket — cay bracket knock-out
    @GetMapping("/{id}/bracket")
    public ApiResponse<BracketResponse> bracket(@PathVariable Integer id) {
        timGiaiCongKhai(id);
        return ApiResponse.ok(knockOutService.getBracket(id));
    }

    // GET /public/tournament/{id}/matches — lich thi dau
    @GetMapping("/{id}/matches")
    public ApiResponse<List<TranDauResponse>> matches(@PathVariable Integer id) {
        timGiaiCongKhai(id);
        List<TranDauResponse> result = tranDauRepository.findByGiaiDauId(id).stream()
                .map(TranDauResponse::from).toList();
        return ApiResponse.ok(result);
    }

    // GET /public/tournament/{id}/top-scorers — vua pha luoi
    @GetMapping("/{id}/top-scorers")
    public ApiResponse<List<ThanhVienDoiResponse>> topScorers(@PathVariable Integer id) {
        GiaiDau giai = timGiaiCongKhai(id);
        List<ThanhVienDoiResponse> result = giai.getDoiBongs().stream()
                .flatMap(d -> d.getThanhViens().stream())
                .filter(tv -> tv.getTongBanThang() != null && tv.getTongBanThang() > 0)
                .sorted((a, b) -> Integer.compare(b.getTongBanThang(), a.getTongBanThang()))
                .limit(10)
                .map(ThanhVienDoiResponse::from)
                .toList();
        return ApiResponse.ok(result);
    }

    // ══════════════════════════════════════════════════════════
    // POST /public/tournament/{id}/register — dang ky doi (yeu cau dang nhap)
    // Gop DangKy + XacNhanDangKy + Checkout cua ban C# thanh 1 buoc REST:
    // tao doi ngay voi DaThanhToan=false, cho Owner xac nhan thanh toan sau.
    // ══════════════════════════════════════════════════════════
    @PostMapping("/{id}/register")
    public ApiResponse<DoiBongResponse> register(@PathVariable Integer id,
                                                  @Valid @RequestBody DangKyDoiRequest request,
                                                  @AuthenticationPrincipal AuthenticatedUser user) {
        yeuCauDangNhap(user);

        GiaiDau giai = giaiDauRepository.findById(id)
                .orElseThrow(() -> new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!"));
        if (!"RegistrationOpen".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Giai dau khong trong thoi gian dang ky!");
        }
        if (doiBongRepository.existsByGiaiDauIdAndDoiTruongId(id, user.userId())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Ban da dang ky giai nay roi!");
        }
        long soDoiDaDK = doiBongRepository.countByGiaiDauIdAndDaThanhToan(id, true);
        if (soDoiDaDK >= giai.getSoDoiToiDa()) {
            throw new TournamentException(TournamentException.Code.GIA_KHONG_DU_DOI, "Giai dau da du doi!");
        }
        if (doiBongRepository.existsByGiaiDauIdAndTenDoiIgnoreCase(id, request.getTenDoi().trim())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Ten doi da ton tai trong giai nay!");
        }

        DoiBong doi = new DoiBong();
        doi.setGiaiDau(giai);
        doi.setDoiTruongId(user.userId());
        doi.setTenDoi(request.getTenDoi().trim());
        doi.setTienKyQuyConLai(giai.getTienKyQuy());
        doi.setDaThanhToan(false);
        doi.setTrangThai("Active");
        doi.setThoiGianTao(LocalDateTime.now());
        doiBongRepository.save(doi);

        notificationService.guiEmailXacNhanDangKy(doi.getId());

        return ApiResponse.ok("Dang ky thanh cong! Cho Owner xac nhan thanh toan.", DoiBongResponse.from(doi));
    }

    // GET /public/tournament/teams/{teamId} — chi tiet doi cua chinh minh
    // (dung cho Checkout/NopDanhSach ben C# — cac trang nay chi co doiId
    // tren URL, khong co giaiId, nen can lookup truc tiep qua doiId).
    @GetMapping("/teams/{teamId}")
    public ApiResponse<DoiBongResponse> teamDetail(@PathVariable Integer teamId,
                                                    @AuthenticationPrincipal AuthenticatedUser user) {
        yeuCauDangNhap(user);
        DoiBong doi = timDoiCuaToi(teamId, user.userId());
        return ApiResponse.ok(DoiBongResponse.from(doi));
    }

    // GET /public/tournament/teams/{teamId}/roster — xem danh sach cau thu (chu doi)
    @GetMapping("/teams/{teamId}/roster")
    public ApiResponse<List<ThanhVienDoiResponse>> roster(@PathVariable Integer teamId,
                                                           @AuthenticationPrincipal AuthenticatedUser user) {
        yeuCauDangNhap(user);
        DoiBong doi = timDoiCuaToi(teamId, user.userId());
        List<ThanhVienDoiResponse> result = thanhVienDoiRepository.findByDoiId(doi.getId()).stream()
                .map(ThanhVienDoiResponse::from).toList();
        return ApiResponse.ok(result);
    }

    // POST /public/tournament/teams/{teamId}/members — them cau thu
    @PostMapping("/teams/{teamId}/members")
    public ApiResponse<ThanhVienDoiResponse> themThanhVien(@PathVariable Integer teamId,
                                                            @Valid @RequestBody ThemThanhVienRequest request,
                                                            @AuthenticationPrincipal AuthenticatedUser user) {
        yeuCauDangNhap(user);
        DoiBong doi = timDoiCuaToi(teamId, user.userId());
        GiaiDau giai = giaiDauRepository.findById(doi.getGiaiDauId()).orElseThrow();

        if (!"RegistrationOpen".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Danh sach da bi khoa!");
        }
        if (thanhVienDoiRepository.existsByDoiIdAndSoAo(teamId, request.getSoAo())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "So ao " + request.getSoAo() + " da co nguoi dung!");
        }

        ThanhVienDoi tv = new ThanhVienDoi();
        tv.setDoi(doi);
        tv.setHoTen(request.getHoTen().trim());
        tv.setSoAo(request.getSoAo());
        tv.setAnhDaiDien(request.getAnhDaiDien());
        tv.setSoTranTreoGio(0);
        tv.setTongBanThang(0);
        tv.setTongTheVang(0);
        tv.setTongTheDo(0);
        thanhVienDoiRepository.save(tv);

        return ApiResponse.ok("Da them cau thu " + tv.getHoTen(), ThanhVienDoiResponse.from(tv));
    }

    // DELETE /public/tournament/teams/{teamId}/members/{memberId} — xoa cau thu
    @DeleteMapping("/teams/{teamId}/members/{memberId}")
    public ApiResponse<Void> xoaThanhVien(@PathVariable Integer teamId, @PathVariable Integer memberId,
                                           @AuthenticationPrincipal AuthenticatedUser user) {
        yeuCauDangNhap(user);
        DoiBong doi = timDoiCuaToi(teamId, user.userId());
        GiaiDau giai = giaiDauRepository.findById(doi.getGiaiDauId()).orElseThrow();

        if (!"RegistrationOpen".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Danh sach da bi khoa!");
        }

        ThanhVienDoi tv = thanhVienDoiRepository.findById(memberId)
                .filter(t -> teamId.equals(t.getDoiId()))
                .orElseThrow(() -> new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay cau thu!"));

        thanhVienDoiRepository.delete(tv);
        return ApiResponse.ok("Da xoa cau thu " + tv.getHoTen(), null);
    }

    // ── Helper ─────────────────────────────────────────────
    private GiaiDau timGiaiCongKhai(Integer id) {
        GiaiDau giai = giaiDauRepository.findDetailById(id)
                .orElseThrow(() -> new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!"));
        if ("Draft".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!");
        }
        return giai;
    }

    private DoiBong timDoiCuaToi(Integer teamId, Integer userId) {
        return doiBongRepository.findByIdAndDoiTruongId(teamId, userId)
                .orElseThrow(() -> new TournamentException(TournamentException.Code.KHONG_CO_QUYEN, "Doi khong thuoc ve ban!"));
    }

    private void yeuCauDangNhap(AuthenticatedUser user) {
        if (user == null) {
            throw new TournamentException(TournamentException.Code.KHONG_CO_QUYEN, "Ban can dang nhap!");
        }
    }

    private GiaiDauResponse enrich(GiaiDauResponse r, GiaiDau g) {
        sanBongRepository.findById(g.getSanBongId()).ifPresent(s -> {
            r.setTenSan(s.getTenSan());
            r.setQuan(s.getQuan());
        });
        userRepository.findById(g.getOwnerId()).ifPresent(u -> {
            r.setOwnerHoTen(u.getHoTen());
            r.setOwnerEmail(u.getEmail());
        });
        r.setSoDoiDaDangKy(doiBongRepository.findByGiaiDauId(g.getId()).size());
        r.setSoDoiDaThanhToan(doiBongRepository.countByGiaiDauIdAndDaThanhToan(g.getId(), true));
        return r;
    }
}
