package com.pitchhub.tournament.controller;

import com.pitchhub.tournament.dto.response.ApiResponse;
import com.pitchhub.tournament.dto.response.BaoCaoResponse;
import com.pitchhub.tournament.dto.response.DoiBongResponse;
import com.pitchhub.tournament.dto.response.GiaiDauResponse;
import com.pitchhub.tournament.dto.response.TranDauResponse;
import com.pitchhub.tournament.exception.TournamentException;
import com.pitchhub.tournament.model.DoiBong;
import com.pitchhub.tournament.model.GiaiDau;
import com.pitchhub.tournament.repository.DoiBongRepository;
import com.pitchhub.tournament.repository.GiaiDauRepository;
import com.pitchhub.tournament.repository.SanBongRepository;
import com.pitchhub.tournament.repository.TranDauRepository;
import com.pitchhub.tournament.repository.UserRepository;
import com.pitchhub.tournament.service.TournamentExcelService;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.time.YearMonth;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;

/** /api/admin/tournament — Admin xem/duyet toan bo giai dau tren he thong. */
@RestController
@RequestMapping("/admin/tournament")
public class AdminTournamentController {

    private final GiaiDauRepository giaiDauRepository;
    private final DoiBongRepository doiBongRepository;
    private final TranDauRepository tranDauRepository;
    private final SanBongRepository sanBongRepository;
    private final UserRepository userRepository;
    private final TournamentExcelService excelService;

    public AdminTournamentController(GiaiDauRepository giaiDauRepository,
                                      DoiBongRepository doiBongRepository,
                                      TranDauRepository tranDauRepository,
                                      SanBongRepository sanBongRepository,
                                      UserRepository userRepository,
                                      TournamentExcelService excelService) {
        this.giaiDauRepository = giaiDauRepository;
        this.doiBongRepository = doiBongRepository;
        this.tranDauRepository = tranDauRepository;
        this.sanBongRepository = sanBongRepository;
        this.userRepository = userRepository;
        this.excelService = excelService;
    }

    // GET /admin/tournament — tat ca giai + KPI + bo loc
    @GetMapping
    public ApiResponse<List<GiaiDauResponse>> index(
            @RequestParam(required = false) String trangThai,
            @RequestParam(required = false) String keyword,
            @RequestParam(required = false) Integer ownerId,
            @RequestParam(required = false) String sapXep) {

        var stream = giaiDauRepository.findAll().stream();

        if (trangThai != null && !trangThai.isBlank()) {
            stream = stream.filter(g -> trangThai.equals(g.getTrangThai()));
        }
        if (keyword != null && !keyword.isBlank()) {
            String kw = keyword.toLowerCase();
            stream = stream.filter(g -> g.getTenGiai().toLowerCase().contains(kw)
                    || sanBongRepository.findById(g.getSanBongId())
                    .map(s -> s.getTenSan().toLowerCase().contains(kw)).orElse(false)
                    || userRepository.findById(g.getOwnerId())
                    .map(u -> u.getHoTen().toLowerCase().contains(kw)).orElse(false));
        }
        if (ownerId != null) {
            stream = stream.filter(g -> ownerId.equals(g.getOwnerId()));
        }

        Comparator<GiaiDau> comparator = switch (sapXep == null ? "" : sapXep) {
            case "cu_nhat" -> Comparator.comparing(GiaiDau::getThoiGianTao);
            case "ten" -> Comparator.comparing(GiaiDau::getTenGiai);
            default -> Comparator.comparing(GiaiDau::getThoiGianTao).reversed();
        };

        List<GiaiDauResponse> result = stream.sorted(comparator)
                .map(g -> enrich(GiaiDauResponse.from(g), g)).toList();

        return ApiResponse.ok(result);
    }

    // GET /admin/tournament/kpi — thay ViewBag KPI cua Index ben C#
    @GetMapping("/kpi")
    public ApiResponse<Kpi> kpi() {
        Kpi kpi = new Kpi();
        kpi.tongGiai = giaiDauRepository.count();
        kpi.choDuyet = giaiDauRepository.findByTrangThai("Draft").size();
        kpi.daDuyet = giaiDauRepository.findByTrangThai("Approved").size();
        kpi.dangDienRa = giaiDauRepository.findByTrangThai("Active").size();
        kpi.dangDangKy = giaiDauRepository.findByTrangThai("RegistrationOpen").size();
        kpi.tongLePhi = tongLePhiDaThu();
        return ApiResponse.ok(kpi);
    }

    // GET /admin/tournament/{id} — chi tiet giai (chi xem, khong sua nghiep vu)
    @GetMapping("/{id}")
    public ApiResponse<GiaiDauResponse> details(@PathVariable Integer id) {
        GiaiDau giai = giaiDauRepository.findDetailById(id)
                .orElseThrow(() -> new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!"));

        GiaiDauResponse response = enrich(GiaiDauResponse.from(giai), giai);
        response.setDoiBongs(giai.getDoiBongs().stream().map(DoiBongResponse::from).toList());
        response.setTranDaus(giai.getTranDaus().stream().map(TranDauResponse::from).toList());
        response.setBangDaus(giai.getBangDaus().stream()
                .map(b -> new GiaiDauResponse.BangDauSummary(b.getId(), b.getTenBang())).toList());
        return ApiResponse.ok(response);
    }

    // POST /admin/tournament/{id}/approve — Draft -> Approved
    @PostMapping("/{id}/approve")
    public ApiResponse<Void> approve(@PathVariable Integer id, @RequestParam(required = false) String ghiChu) {
        GiaiDau giai = giaiDauRepository.findById(id)
                .orElseThrow(() -> new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!"));
        if (!"Draft".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Chi phe duyet giai o trang thai Draft!");
        }

        giai.setTrangThai("Approved");
        giaiDauRepository.save(giai);

        return ApiResponse.ok("Da phe duyet giai '" + giai.getTenGiai() + "'. Owner co the mo dang ky.", null);
    }

    // POST /admin/tournament/{id}/reject — Draft -> tu choi va xoa
    @PostMapping("/{id}/reject")
    public ApiResponse<Void> reject(@PathVariable Integer id, @RequestParam String lyDo) {
        GiaiDau giai = giaiDauRepository.findById(id)
                .orElseThrow(() -> new TournamentException(TournamentException.Code.GIAI_KHONG_TON_TAI, "Khong tim thay giai!"));
        if (!"Draft".equals(giai.getTrangThai())) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Chi tu choi giai o trang thai Draft!");
        }
        if (lyDo == null || lyDo.isBlank()) {
            throw new TournamentException(TournamentException.Code.TRANG_THAI_KHONG_HOP_LE, "Can nhap ly do tu choi!");
        }

        String tenGiai = giai.getTenGiai();
        giaiDauRepository.delete(giai);

        return ApiResponse.ok("Da tu choi va xoa giai '" + tenGiai + "'.", null);
    }

    // GET /admin/tournament/{id}/excel — Admin cung co the tai Excel
    @GetMapping("/{id}/excel")
    public ResponseEntity<byte[]> excel(@PathVariable Integer id) {
        var file = excelService.exportDoiSoat(id);
        return ResponseEntity.ok()
                .contentType(MediaType.parseMediaType("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"))
                .header(HttpHeaders.CONTENT_DISPOSITION, "attachment; filename=\"" + file.fileName() + "\"")
                .body(file.bytes());
    }

    // GET /admin/tournament/report — bao cao doanh thu giai dau
    @GetMapping("/report")
    public ApiResponse<BaoCaoResponse> report() {
        BaoCaoResponse response = new BaoCaoResponse();
        LocalDateTime now = LocalDateTime.now();

        for (int i = 5; i >= 0; i--) {
            YearMonth ym = YearMonth.from(now.minusMonths(i));
            LocalDateTime bd = ym.atDay(1).atStartOfDay();
            LocalDateTime kt = bd.plusMonths(1);

            long soGiai = giaiDauRepository.findAll().stream()
                    .filter(g -> !g.getThoiGianTao().isBefore(bd) && g.getThoiGianTao().isBefore(kt)).count();

            List<DoiBong> doiThanhToanThang = doiBongRepository.findByThoiGianThanhToanBetween(bd, kt);

            BigDecimal doanhThu = doiThanhToanThang.stream()
                    .map(d -> giaiDauRepository.findById(d.getGiaiDauId()).map(GiaiDau::getLePhiGiai).orElse(BigDecimal.ZERO))
                    .reduce(BigDecimal.ZERO, BigDecimal::add);

            BaoCaoResponse.ThangKe thangKe = new BaoCaoResponse.ThangKe();
            thangKe.thang = ym.format(DateTimeFormatter.ofPattern("MM/yyyy"));
            thangKe.soGiai = soGiai;
            thangKe.soDoiThanhToan = doiThanhToanThang.size();
            thangKe.doanhThu = doanhThu;
            response.bieu6Thang.add(thangKe);
        }

        response.topOwner = topOwner();
        response.tongGiaiDau = giaiDauRepository.findAll().stream().filter(g -> !"Draft".equals(g.getTrangThai())).count();
        response.giaiHoanThanh = giaiDauRepository.findByTrangThai("Finished").size();
        response.tongDoi = doiBongRepository.findAll().stream().filter(d -> Boolean.TRUE.equals(d.getDaThanhToan())).count();
        response.tongTranDau = tranDauRepository.findAll().stream().filter(t -> "Closed".equals(t.getTrangThai())).count();
        response.tongDoanhThu = tongLePhiDaThu();

        return ApiResponse.ok(response);
    }

    // ── Helper ─────────────────────────────────────────────
    private BigDecimal tongLePhiDaThu() {
        return doiBongRepository.findAll().stream()
                .filter(d -> Boolean.TRUE.equals(d.getDaThanhToan()))
                .map(d -> giaiDauRepository.findById(d.getGiaiDauId()).map(GiaiDau::getLePhiGiai).orElse(BigDecimal.ZERO))
                .reduce(BigDecimal.ZERO, BigDecimal::add);
    }

    private List<BaoCaoResponse.TopOwnerRow> topOwner() {
        var giaiKhongDraft = giaiDauRepository.findAll().stream()
                .filter(g -> !"Draft".equals(g.getTrangThai())).toList();

        var grouped = giaiKhongDraft.stream()
                .collect(java.util.stream.Collectors.groupingBy(GiaiDau::getOwnerId));

        List<BaoCaoResponse.TopOwnerRow> result = new ArrayList<>();
        for (var e : grouped.entrySet()) {
            var user = userRepository.findById(e.getKey()).orElse(null);
            if (user == null) continue;
            BaoCaoResponse.TopOwnerRow row = new BaoCaoResponse.TopOwnerRow();
            row.hoTen = user.getHoTen();
            row.email = user.getEmail();
            row.soGiai = e.getValue().size();
            row.soActive = e.getValue().stream().filter(g -> "Active".equals(g.getTrangThai())).count();
            result.add(row);
        }
        return result.stream().sorted(Comparator.comparingLong((BaoCaoResponse.TopOwnerRow r) -> r.soGiai).reversed())
                .limit(10).toList();
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

    public static class Kpi {
        public long tongGiai;
        public long choDuyet;
        public long daDuyet;
        public long dangDienRa;
        public long dangDangKy;
        public BigDecimal tongLePhi;
    }
}
