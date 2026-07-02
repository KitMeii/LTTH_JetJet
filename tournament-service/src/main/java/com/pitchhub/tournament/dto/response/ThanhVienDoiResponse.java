package com.pitchhub.tournament.dto.response;

import com.pitchhub.tournament.model.ThanhVienDoi;
import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;

@Getter
@Setter
@NoArgsConstructor
public class ThanhVienDoiResponse {
    private Integer id;
    private Integer doiId;
    private String hoTen;
    private Integer soAo;
    private String anhDaiDien;
    private Integer soTranTreoGio;
    private Integer tongBanThang;
    private Integer tongTheVang;
    private Integer tongTheDo;

    public static ThanhVienDoiResponse from(ThanhVienDoi tv) {
        ThanhVienDoiResponse r = new ThanhVienDoiResponse();
        r.id = tv.getId();
        r.doiId = tv.getDoiId();
        r.hoTen = tv.getHoTen();
        r.soAo = tv.getSoAo();
        r.anhDaiDien = tv.getAnhDaiDien();
        r.soTranTreoGio = tv.getSoTranTreoGio();
        r.tongBanThang = tv.getTongBanThang();
        r.tongTheVang = tv.getTongTheVang();
        r.tongTheDo = tv.getTongTheDo();
        return r;
    }
}
