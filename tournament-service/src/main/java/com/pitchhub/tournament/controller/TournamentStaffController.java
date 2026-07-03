package com.pitchhub.tournament.controller;

import com.pitchhub.tournament.dto.request.GhiSuKienRequest;
import com.pitchhub.tournament.dto.request.KetThucTranRequest;
import com.pitchhub.tournament.dto.response.ApiResponse;
import com.pitchhub.tournament.dto.response.TranDauResponse;
import com.pitchhub.tournament.exception.TournamentException;
import com.pitchhub.tournament.model.SuKienTran;
import com.pitchhub.tournament.model.ThanhVienDoi;
import com.pitchhub.tournament.model.TranDau;
import com.pitchhub.tournament.repository.*;
import com.pitchhub.tournament.security.AuthenticatedUser;
import com.pitchhub.tournament.service.KnockOutService;
import com.pitchhub.tournament.service.StandingService;
import com.pitchhub.tournament.service.SuspensionService;
import jakarta.validation.Valid;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.List;

/**
 * /api/tournament/staff — Staff dieu hanh tran dau duoc phan cong.
 * Tuong duong TournamentStaffController.cs (da bo phan SignalR broadcast
 * theo yeu cau — chi con REST, frontend tu polling neu can realtime).
 */
@RestController
@RequestMapping("/tournament/staff")
public class TournamentStaffController {

    private final TranDauRepository tranDauRepository;
    private final SuKienTranRepository suKienTranRepository;
    private final ThanhVienDoiRepository thanhVienDoiRepository;
    private final StaffSanPhanCongRepository staffSanPhanCongRepository;
    private final SanBongRepository sanBongRepository;
    private final UserRepository userRepository;
    private final SuspensionService suspensionService;
    private final StandingService standingService;
    private final KnockOutService knockOutService;

    public TournamentStaffController(TranDauRepository tranDauRepository,
                                      SuKienTranRepository suKienTranRepository,
                                      ThanhVienDoiRepository thanhVienDoiRepository,
                                      StaffSanPhanCongRepository staffSanPhanCongRepository,
                                      SanBongRepository sanBongRepository,
                                      UserRepository userRepository,
                                      SuspensionService suspensionService,
                                      StandingService standingService,
                                      KnockOutService knockOutService) {
        this.tranDauRepository = tranDauRepository;
        this.suKienTranRepository = suKienTranRepository;
        this.thanhVienDoiRepository = thanhVienDoiRepository;
        this.staffSanPhanCongRepository = staffSanPhanCongRepository;
        this.sanBongRepository = sanBongRepository;
        this.userRepository = userRepository;
        this.suspensionService = suspensionService;
        this.standingService = standingService;
        this.knockOutService = knockOutService;
    }

    // GET /tournament/staff/matches — danh sach tran phan cong (loc: hom_nay | tuan_nay | tat ca)
    @GetMapping("/matches")
    public ApiResponse<List<TranDauResponse>> matches(
            @RequestParam(required = false, defaultValue = "hom_nay") String loc,
            @RequestParam(required = false) String trangThai,
            @AuthenticationPrincipal AuthenticatedUser user) {

        List<Integer> sanIds = sanDuocGiao(user.userId());

        List<TranDau> result;
        if ("hom_nay".equals(loc)) {
            LocalDateTime homNay = LocalDate.now().atStartOfDay();
            result = tranDauRepository.findChoStaffTrongKhoang(sanIds, homNay, homNay.plusDays(1), trangThai);
        } else if ("tuan_nay".equals(loc)) {
            LocalDate today = LocalDate.now();
            LocalDateTime dauTuan = today.minusDays(today.getDayOfWeek().getValue() % 7).atStartOfDay();
            result = tranDauRepository.findChoStaffTrongKhoang(sanIds, dauTuan, dauTuan.plusDays(7), trangThai);
        } else {
            result = tranDauRepository.findChoStaff(sanIds, trangThai);
        }

        return ApiResponse.ok(result.stream().map(TranDauResponse::from).toList());
    }

    // GET /tournament/staff/matches/{matchId} — chi tiet tran
    @GetMapping("/matches/{matchId}")
    public ApiResponse<TranDauResponse> matchDetail(@PathVariable Integer matchId,
                                                     @AuthenticationPrincipal AuthenticatedUser user) {
        TranDau tran = timTranDuocPhanCong(matchId, user.userId());
        return ApiResponse.ok(enrichTran(TranDauResponse.from(tran), tran));
    }

    // POST /tournament/staff/matches/{matchId}/checkin — check-in, chuyen InProgress
    @PostMapping("/matches/{matchId}/checkin")
    public ApiResponse<TranDauResponse> checkin(@PathVariable Integer matchId, @AuthenticationPrincipal AuthenticatedUser user) {
        TranDau tran = timTranDuocPhanCong(matchId, user.userId());

        if ("Closed".equals(tran.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Tran nay da ket thuc!");
        }
        if (tran.getStaffPhuTrachId() == null) {
            tran.setStaffPhuTrachId(user.userId());
        }
        if ("Scheduled".equals(tran.getTrangThai())) {
            tran.setTrangThai("InProgress");
        }
        tranDauRepository.save(tran);

        return ApiResponse.ok(enrichTran(TranDauResponse.from(tran), tran));
    }

    // POST /tournament/staff/matches/{matchId}/events — ghi su kien (ban thang/the vang/the do)
    @PostMapping("/matches/{matchId}/events")
    public ApiResponse<EventResult> ghiSuKien(@PathVariable Integer matchId,
                                               @Valid @RequestBody GhiSuKienRequest request,
                                               @AuthenticationPrincipal AuthenticatedUser user) {
        TranDau tran = timTranDuocPhanCong(matchId, user.userId());
        if (!"InProgress".equals(tran.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Tran khong dang dien ra!");
        }

        if (request.getThanhVienId() != null
                && ("BanThang".equals(request.getLoaiSuKien()) || "TheVang".equals(request.getLoaiSuKien())
                || "TheDo".equals(request.getLoaiSuKien()))) {
            ThanhVienDoi tv = thanhVienDoiRepository.findById(request.getThanhVienId()).orElse(null);
            if (tv != null && tv.getSoTranTreoGio() > 0 && !"BanThang".equals(request.getLoaiSuKien())) {
                throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE,
                        tv.getHoTen() + " dang bi treo gio!");
            }
        }

        String loaiThucTe = request.getLoaiSuKien();
        if ("TheVang".equals(request.getLoaiSuKien()) && request.getThanhVienId() != null) {
            long soVangTranNay = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(
                    matchId, "TheVang", request.getDoiId());
            if (soVangTranNay >= 1) loaiThucTe = "TheVangLan2";
        }

        SuKienTran sk = new SuKienTran();
        sk.setTranDau(tran);
        if (request.getThanhVienId() != null) {
            ThanhVienDoi tv = thanhVienDoiRepository.findById(request.getThanhVienId()).orElse(null);
            sk.setThanhVien(tv);
        }
        var doi = new com.pitchhub.tournament.model.DoiBong();
        doi.setId(request.getDoiId());
        sk.setDoi(doi);
        sk.setLoaiSuKien(loaiThucTe);
        sk.setPhut(request.getPhut());
        sk.setGhiChu(request.getGhiChu() != null ? request.getGhiChu().trim() : null);
        sk.setThoiGianGhi(LocalDateTime.now());
        suKienTranRepository.save(sk);

        if (request.getThanhVienId() != null) {
            String loaiSuKienCuoi = loaiThucTe;
            thanhVienDoiRepository.findById(request.getThanhVienId()).ifPresent(tv -> {
                if ("BanThang".equals(loaiSuKienCuoi)) tv.setTongBanThang(tv.getTongBanThang() + 1);
                if ("TheVang".equals(loaiSuKienCuoi)) tv.setTongTheVang(tv.getTongTheVang() + 1);
                if ("TheDo".equals(loaiSuKienCuoi) || "TheVangLan2".equals(loaiSuKienCuoi)) tv.setTongTheDo(tv.getTongTheDo() + 1);
                thanhVienDoiRepository.save(tv);
            });
        }

        long tysoNha = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(matchId, "BanThang", tran.getDoiNhaId());
        long tysoKhach = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(matchId, "BanThang", tran.getDoiKhachId());

        EventResult result = new EventResult();
        result.loaiThucTe = loaiThucTe;
        result.tysoNha = tysoNha;
        result.tysoKhach = tysoKhach;

        String message = "TheVangLan2".equals(loaiThucTe) ? "The vang lan 2 — tu dong the do!" : "Da ghi su kien";
        return ApiResponse.ok(message, result);
    }

    // DELETE /tournament/staff/matches/{matchId}/events/{eventId} — huy ban thang nham
    @DeleteMapping("/matches/{matchId}/events/{eventId}")
    public ApiResponse<EventResult> huyBanThang(@PathVariable Integer matchId, @PathVariable Integer eventId,
                                                 @AuthenticationPrincipal AuthenticatedUser user) {
        TranDau tran = timTranDuocPhanCong(matchId, user.userId());

        suKienTranRepository.findById(eventId).ifPresent(sk -> {
            if (sk.getThanhVien() != null && "BanThang".equals(sk.getLoaiSuKien())) {
                ThanhVienDoi tv = sk.getThanhVien();
                tv.setTongBanThang(Math.max(0, tv.getTongBanThang() - 1));
                thanhVienDoiRepository.save(tv);
            }
            suKienTranRepository.delete(sk);
        });

        long tysoNha = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(matchId, "BanThang", tran.getDoiNhaId());
        long tysoKhach = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(matchId, "BanThang", tran.getDoiKhachId());

        EventResult result = new EventResult();
        result.tysoNha = tysoNha;
        result.tysoKhach = tysoKhach;
        return ApiResponse.ok(result);
    }

    // POST /tournament/staff/matches/{matchId}/finish — soft-lock, tra ve tyso de doi truong xac nhan
    @PostMapping("/matches/{matchId}/finish")
    public ApiResponse<EventResult> finish(@PathVariable Integer matchId,
                                            @RequestBody(required = false) KetThucTranRequest request,
                                            @AuthenticationPrincipal AuthenticatedUser user) {
        TranDau tran = timTranDuocPhanCong(matchId, user.userId());
        if (!"InProgress".equals(tran.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Tran khong dang dien ra!");
        }

        EventResult result = new EventResult();
        result.tysoNha = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(matchId, "BanThang", tran.getDoiNhaId());
        result.tysoKhach = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(matchId, "BanThang", tran.getDoiKhachId());
        return ApiResponse.ok(result);
    }

    // POST /tournament/staff/matches/{matchId}/confirm — chot ket qua tran (Closed)
    // Tra ve luon BXH moi nhat — thay the cho SignalR BroadcastBXH da bo (REST thuan).
    @PostMapping("/matches/{matchId}/confirm")
    public ApiResponse<ConfirmResult> confirm(@PathVariable Integer matchId,
                                               @AuthenticationPrincipal AuthenticatedUser user) {
        TranDau tran = timTranDuocPhanCong(matchId, user.userId());
        if (!"InProgress".equals(tran.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Tran khong dang dien ra!");
        }

        long tysoNha = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(matchId, "BanThang", tran.getDoiNhaId());
        long tysoKhach = suKienTranRepository.countByTranDauIdAndLoaiSuKienAndDoiId(matchId, "BanThang", tran.getDoiKhachId());

        tran.setBanThangNha((int) tysoNha);
        tran.setBanThangKhach((int) tysoKhach);
        tran.setTrangThai("Closed");
        tranDauRepository.save(tran);

        suspensionService.xuLyTreoGio(tran.getGiaiDauId());

        // HOAN THIEN: neu la tran knock-out, dien doi thang vao vong ke tiep
        if (!"VongBang".equals(tran.getLoaiVong())) {
            knockOutService.capNhatDoiKnockOut(matchId);
        }

        ConfirmResult result = new ConfirmResult();
        result.tranDau = enrichTran(TranDauResponse.from(tran), tran);
        result.standings = standingService.getStandings(tran.getGiaiDauId());

        return ApiResponse.ok("Da chot tran! Ket qua: " + tran.getBanThangNha() + " - " + tran.getBanThangKhach(), result);
    }

    // ── Helper ─────────────────────────────────────────────
    private List<Integer> sanDuocGiao(Integer staffId) {
        return staffSanPhanCongRepository.findByStaffId(staffId).stream()
                .map(com.pitchhub.tournament.model.StaffSanPhanCong::getSanBongId).toList();
    }

    /** Dien them thong tin ma frontend C# can hien thi tren man hinh Staff. */
    private TranDauResponse enrichTran(TranDauResponse r, TranDau tran) {
        sanBongRepository.findById(tran.getGiaiDau().getSanBongId())
                .ifPresent(s -> r.setSanBongTenSan(s.getTenSan()));
        r.setGiaiTenGiai(tran.getGiaiDau().getTenGiai());
        r.setGiaiTienPhatTheVang(tran.getGiaiDau().getTienPhatTheVang());
        r.setGiaiTienPhatTheDo(tran.getGiaiDau().getTienPhatTheDo());
        if (tran.getStaffPhuTrachId() != null) {
            userRepository.findById(tran.getStaffPhuTrachId())
                    .ifPresent(u -> r.setStaffPhuTrachHoTen(u.getHoTen()));
        }
        return r;
    }

    private TranDau timTranDuocPhanCong(Integer matchId, Integer staffId) {
        TranDau tran = tranDauRepository.findByIdWithGiaiDau(matchId)
                .orElseThrow(() -> new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay tran!"));

        List<Integer> sanIds = sanDuocGiao(staffId);
        if (!sanIds.contains(tran.getGiaiDau().getSanBongId())) {
            throw new TournamentException(TournamentException.Code.KHONG_CO_QUYEN, "Ban khong duoc phan cong san nay!");
        }
        return tran;
    }

    public static class EventResult {
        public String loaiThucTe;
        public long tysoNha;
        public long tysoKhach;
    }

    public static class ConfirmResult {
        public TranDauResponse tranDau;
        public java.util.Map<Integer, List<com.pitchhub.tournament.dto.response.StandingRowResponse>> standings;
    }
}
