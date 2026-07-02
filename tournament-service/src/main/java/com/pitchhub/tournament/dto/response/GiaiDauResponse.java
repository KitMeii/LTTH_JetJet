package com.pitchhub.tournament.dto.response;

import com.pitchhub.tournament.model.GiaiDau;
import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.util.List;

@Getter
@Setter
@NoArgsConstructor
public class GiaiDauResponse {
    private Integer id;
    private String tenGiai;
    private String moTa;
    private Integer sanBongId;
    private String tenSan;
    private String quan;
    private Integer ownerId;
    private String ownerHoTen;
    private String ownerEmail;
    private Integer soDoiToiDa;
    private Integer soBang;
    private BigDecimal lePhiGiai;
    private BigDecimal tienKyQuy;
    private BigDecimal tienPhatTheVang;
    private BigDecimal tienPhatTheDo;
    private Integer soTranTreoGioTheDo;
    private Integer soTheVangTichLuy;
    private LocalDateTime ngayBatDau;
    private LocalDateTime ngayKetThuc;
    private LocalDateTime thoiGianTao;
    private LocalDateTime thoiGianDongDanhSach;
    private String trangThai;
    private long soDoiDaDangKy;
    private long soDoiDaThanhToan;

    /** Chi dien khi goi Details — danh sach bang/doi/tran day du. */
    private List<BangDauSummary> bangDaus;
    private List<DoiBongResponse> doiBongs;
    private List<TranDauResponse> tranDaus;

    public static GiaiDauResponse from(GiaiDau g) {
        GiaiDauResponse r = new GiaiDauResponse();
        r.id = g.getId();
        r.tenGiai = g.getTenGiai();
        r.moTa = g.getMoTa();
        r.sanBongId = g.getSanBongId();
        r.ownerId = g.getOwnerId();
        r.soDoiToiDa = g.getSoDoiToiDa();
        r.soBang = g.getSoBang();
        r.lePhiGiai = g.getLePhiGiai();
        r.tienKyQuy = g.getTienKyQuy();
        r.tienPhatTheVang = g.getTienPhatTheVang();
        r.tienPhatTheDo = g.getTienPhatTheDo();
        r.soTranTreoGioTheDo = g.getSoTranTreoGioTheDo();
        r.soTheVangTichLuy = g.getSoTheVangTichLuy();
        r.ngayBatDau = g.getNgayBatDau();
        r.ngayKetThuc = g.getNgayKetThuc();
        r.thoiGianTao = g.getThoiGianTao();
        r.thoiGianDongDanhSach = g.getThoiGianDongDanhSach();
        r.trangThai = g.getTrangThai();
        return r;
    }

    @Getter
    @Setter
    @NoArgsConstructor
    public static class BangDauSummary {
        private Integer id;
        private String tenBang;

        public BangDauSummary(Integer id, String tenBang) {
            this.id = id;
            this.tenBang = tenBang;
        }
    }
}
