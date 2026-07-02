package com.pitchhub.tournament.controller;

import com.pitchhub.tournament.dto.request.*;
import com.pitchhub.tournament.dto.response.*;
import com.pitchhub.tournament.exception.TournamentException;
import com.pitchhub.tournament.model.GiaiDau;
import com.pitchhub.tournament.model.SanBong;
import com.pitchhub.tournament.repository.*;
import com.pitchhub.tournament.security.AuthenticatedUser;
import com.pitchhub.tournament.service.KnockOutService;
import com.pitchhub.tournament.service.StandingService;
import com.pitchhub.tournament.service.TournamentExcelService;
import com.pitchhub.tournament.service.TournamentService;
import jakarta.validation.Valid;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.Map;

/**
 * /api/tournament/owner — Owner quan ly vong doi giai dau cua minh.
 * Tuong duong TournamentController.cs. Chi role Owner (xem SecurityConfig).
 */
@RestController
@RequestMapping("/tournament/owner")
public class TournamentController {

    private final GiaiDauRepository giaiDauRepository;
    private final DoiBongRepository doiBongRepository;
    private final SanBongRepository sanBongRepository;
    private final UserRepository userRepository;
    private final TournamentService tournamentService;
    private final StandingService standingService;
    private final KnockOutService knockOutService;
    private final TournamentExcelService excelService;

    public TournamentController(GiaiDauRepository giaiDauRepository,
                                 DoiBongRepository doiBongRepository,
                                 SanBongRepository sanBongRepository,
                                 UserRepository userRepository,
                                 TournamentService tournamentService,
                                 StandingService standingService,
                                 KnockOutService knockOutService,
                                 TournamentExcelService excelService) {
        this.giaiDauRepository = giaiDauRepository;
        this.doiBongRepository = doiBongRepository;
        this.sanBongRepository = sanBongRepository;
        this.userRepository = userRepository;
        this.tournamentService = tournamentService;
        this.standingService = standingService;
        this.knockOutService = knockOutService;
        this.excelService = excelService;
    }

    // POST /tournament/owner — tao giai moi
    @PostMapping
    public ApiResponse<GiaiDauResponse> create(@Valid @RequestBody TaoGiaiRequest request,
                                                @AuthenticationPrincipal AuthenticatedUser user) {
        GiaiDau giai = tournamentService.taoGiaiDau(request, user.userId());
        return ApiResponse.ok("Tao giai '" + giai.getTenGiai() + "' thanh cong!", GiaiDauResponse.from(giai));
    }

    // GET /tournament/owner/my-tournaments — giai cua toi
    @GetMapping("/my-tournaments")
    public ApiResponse<List<GiaiDauResponse>> myTournaments(@AuthenticationPrincipal AuthenticatedUser user) {
        List<GiaiDauResponse> result = giaiDauRepository.findByOwnerId(user.userId()).stream()
                .sorted((a, b) -> b.getThoiGianTao().compareTo(a.getThoiGianTao()))
                .map(g -> enrich(GiaiDauResponse.from(g), g))
                .toList();
        return ApiResponse.ok(result);
    }

    // GET /tournament/owner/{id} — chi tiet giai cua toi
    @GetMapping("/{id}")
    public ApiResponse<GiaiDauResponse> details(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        GiaiDau giai = timGiaiCuaToi(id, user.userId());
        GiaiDauResponse response = enrich(GiaiDauResponse.from(giai), giai);
        response.setDoiBongs(giai.getDoiBongs().stream().map(DoiBongResponse::from).toList());
        response.setTranDaus(giai.getTranDaus().stream().map(TranDauResponse::from).toList());
        response.setBangDaus(giai.getBangDaus().stream()
                .map(b -> new GiaiDauResponse.BangDauSummary(b.getId(), b.getTenBang())).toList());
        return ApiResponse.ok(response);
    }

    // GET /tournament/owner/{id}/standings
    @GetMapping("/{id}/standings")
    public ApiResponse<Map<Integer, List<StandingRowResponse>>> standings(
            @PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        timGiaiCuaToi(id, user.userId());
        return ApiResponse.ok(standingService.getStandings(id));
    }

    // POST /tournament/owner/{id}/open-registration — mo dang ky
    @PostMapping("/{id}/open-registration")
    public ApiResponse<Void> openRegistration(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        tournamentService.moiDangKy(id, user.userId());
        return ApiResponse.ok("Da mo dang ky!", null);
    }

    // POST /tournament/owner/{id}/close-registration — dong dang ky
    @PostMapping("/{id}/close-registration")
    public ApiResponse<Void> closeRegistration(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        tournamentService.dongDangKy(id, user.userId());
        return ApiResponse.ok("Da dong dang ky! Tien hanh chia bang.", null);
    }

    // GET /tournament/owner/{id}/teams — danh sach doi (kem trang thai thanh toan)
    @GetMapping("/{id}/teams")
    public ApiResponse<List<DoiBongResponse>> teams(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        timGiaiCuaToi(id, user.userId());
        List<DoiBongResponse> result = doiBongRepository.findByGiaiDauId(id).stream()
                .map(DoiBongResponse::from).toList();
        return ApiResponse.ok(result);
    }

    // POST /tournament/owner/{id}/assign-groups — chia bang (drag & drop tung doi)
    @PostMapping("/{id}/assign-groups")
    public ApiResponse<Void> assignGroups(@PathVariable Integer id, @Valid @RequestBody GanBangRequest request,
                                           @AuthenticationPrincipal AuthenticatedUser user) {
        timGiaiCuaToi(id, user.userId());
        tournamentService.ganDoiVaoBang(request.getDoiId(), request.getBangId(), user.userId());
        return ApiResponse.ok("Da gan doi vao bang!", null);
    }

    // POST /tournament/owner/teams/{doiId}/assign-group?bangId= — bien the khong
    // can giaiId tren URL (tuong duong GanBang(doiId, bangId) ben C#, ta tu suy
    // ra giai qua DoiBong.giaiDau ben trong TournamentService.ganDoiVaoBang()).
    @PostMapping("/teams/{doiId}/assign-group")
    public ApiResponse<Void> assignGroupByTeam(@PathVariable Integer doiId,
                                                @RequestParam(required = false) Integer bangId,
                                                @AuthenticationPrincipal AuthenticatedUser user) {
        tournamentService.ganDoiVaoBang(doiId, bangId, user.userId());
        return ApiResponse.ok(null);
    }

    // ══════════════════════════════════════════════════════════
    // HOAN THIEN: xac nhan thanh toan (truoc day chi la comment "PATCH")
    // ══════════════════════════════════════════════════════════
    @PostMapping("/{id}/teams/{teamId}/confirm-payment")
    public ApiResponse<Void> confirmPayment(@PathVariable Integer id, @PathVariable Integer teamId,
                                             @RequestBody(required = false) XacNhanThanhToanRequest request,
                                             @AuthenticationPrincipal AuthenticatedUser user) {
        tournamentService.xacNhanThanhToan(id, teamId, user.userId());
        return ApiResponse.ok("Da xac nhan thanh toan cho doi!", null);
    }

    // POST /tournament/owner/{id}/start — khoi tao (sinh lich vong bang)
    @PostMapping("/{id}/start")
    public ApiResponse<Void> start(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        tournamentService.khoiTaoGiai(id, user.userId());
        return ApiResponse.ok("Khoi tao thanh cong! Email lich dau da gui cho cac doi.", null);
    }

    // POST /tournament/owner/{id}/generate-knockout — HOAN THIEN: sinh vong knock-out
    // (KnockOutService.cs ban goc da viet nhung khong bao gio duoc goi — noi day o day)
    @PostMapping("/{id}/generate-knockout")
    public ApiResponse<Void> generateKnockout(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        GiaiDau giai = timGiaiCuaToi(id, user.userId());
        long tranBangChuaXong = giai.getTranDaus().stream()
                .filter(t -> "VongBang".equals(t.getLoaiVong()) && !"Closed".equals(t.getTrangThai())).count();
        if (tranBangChuaXong > 0) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                    "Con " + tranBangChuaXong + " tran vong bang chua ket thuc!");
        }
        knockOutService.sinhVongKnockOut(id);
        return ApiResponse.ok("Da sinh lich vong knock-out!", null);
    }

    // GET /tournament/owner/{id}/bracket
    @GetMapping("/{id}/bracket")
    public ApiResponse<BracketResponse> bracket(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        timGiaiCuaToi(id, user.userId());
        return ApiResponse.ok(knockOutService.getBracket(id));
    }

    // POST /tournament/owner/{id}/finish — ket thuc giai
    @PostMapping("/{id}/finish")
    public ApiResponse<Void> finish(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        tournamentService.ketThucGiai(id, user.userId());
        return ApiResponse.ok("Giai ket thuc! Tai Excel doi soat ben duoi.", null);
    }

    // POST /tournament/owner/{id}/incident — xu ly su co doi bo cuoc
    @PostMapping("/{id}/incident")
    public ApiResponse<Void> incident(@PathVariable Integer id, @Valid @RequestBody IncidentRequest request,
                                       @AuthenticationPrincipal AuthenticatedUser user) {
        timGiaiCuaToi(id, user.userId());
        tournamentService.xuLySuCo(request.getTranDauId(), request.getDoiBoCuocId(), request.getLyDo(), user.userId());
        return ApiResponse.ok("Da xu ly su co. Doi vi pham thua 0-3.", null);
    }

    // POST /tournament/owner/matches/{tranDauId}/incident — bien the khong can
    // giaiId tren URL (tuong duong XuLySuCo(tranDauId, doiBoCuocId, lyDo) ben C#
    // — TournamentService.xuLySuCo() da tu validate quyen so huu qua tran.GiaiDau).
    @PostMapping("/matches/{tranDauId}/incident")
    public ApiResponse<Void> incidentByMatch(@PathVariable Integer tranDauId,
                                              @RequestParam Integer doiBoCuocId,
                                              @RequestParam String lyDo,
                                              @AuthenticationPrincipal AuthenticatedUser user) {
        tournamentService.xuLySuCo(tranDauId, doiBoCuocId, lyDo, user.userId());
        return ApiResponse.ok(null);
    }

    // GET /tournament/owner/{id}/excel — xuat Excel doi soat
    @GetMapping("/{id}/excel")
    public ResponseEntity<byte[]> excel(@PathVariable Integer id, @AuthenticationPrincipal AuthenticatedUser user) {
        timGiaiCuaToi(id, user.userId());
        var file = excelService.exportDoiSoat(id);
        return ResponseEntity.ok()
                .contentType(MediaType.parseMediaType("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"))
                .header(HttpHeaders.CONTENT_DISPOSITION, "attachment; filename=\"" + file.fileName() + "\"")
                .body(file.bytes());
    }

    // ── Helper ─────────────────────────────────────────────
    private GiaiDau timGiaiCuaToi(Integer id, Integer ownerId) {
        GiaiDau giai = giaiDauRepository.findDetailById(id)
                .orElseThrow(() -> new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!"));
        if (!giai.getOwnerId().equals(ownerId)) {
            throw new TournamentException(TournamentException.Code.KHONG_CO_QUYEN, "Ban khong quan ly giai nay!");
        }
        return giai;
    }

    private GiaiDauResponse enrich(GiaiDauResponse r, GiaiDau g) {
        SanBong san = sanBongRepository.findById(g.getSanBongId()).orElse(null);
        if (san != null) {
            r.setTenSan(san.getTenSan());
            r.setQuan(san.getQuan());
        }
        userRepository.findById(g.getOwnerId()).ifPresent(u -> {
            r.setOwnerHoTen(u.getHoTen());
            r.setOwnerEmail(u.getEmail());
        });
        r.setSoDoiDaDangKy(doiBongRepository.findByGiaiDauId(g.getId()).size());
        r.setSoDoiDaThanhToan(doiBongRepository.countByGiaiDauIdAndDaThanhToan(g.getId(), true));
        return r;
    }
}
