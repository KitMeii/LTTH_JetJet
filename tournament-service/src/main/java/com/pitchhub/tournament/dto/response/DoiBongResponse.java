package com.pitchhub.tournament.dto.response;

import com.pitchhub.tournament.model.DoiBong;
import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.util.List;

@Getter
@Setter
@NoArgsConstructor
public class DoiBongResponse {
    private Integer id;
    private Integer giaiDauId;
    private Integer bangId;
    private String tenBang;
    private Integer doiTruongId;
    private String doiTruongHoTen;
    private String doiTruongEmail;
    private String tenDoi;
    private String logoUrl;
    private BigDecimal tienKyQuyConLai;
    private Boolean daThanhToan;
    private LocalDateTime thoiGianThanhToan;
    private String trangThai;
    private LocalDateTime thoiGianTao;
    private List<ThanhVienDoiResponse> thanhViens;

    public static DoiBongResponse from(DoiBong d) {
        DoiBongResponse r = new DoiBongResponse();
        r.id = d.getId();
        r.giaiDauId = d.getGiaiDauId();
        r.bangId = d.getBangId();
        r.tenBang = d.getBang() != null ? d.getBang().getTenBang() : null;
        r.doiTruongId = d.getDoiTruongId();
        r.tenDoi = d.getTenDoi();
        r.logoUrl = d.getLogoUrl();
        r.tienKyQuyConLai = d.getTienKyQuyConLai();
        r.daThanhToan = d.getDaThanhToan();
        r.thoiGianThanhToan = d.getThoiGianThanhToan();
        r.trangThai = d.getTrangThai();
        r.thoiGianTao = d.getThoiGianTao();
        r.thanhViens = d.getThanhViens().stream().map(ThanhVienDoiResponse::from).toList();
        return r;
    }
}
