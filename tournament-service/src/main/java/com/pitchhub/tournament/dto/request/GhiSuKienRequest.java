package com.pitchhub.tournament.dto.request;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class GhiSuKienRequest {
    private Integer thanhVienId;

    @NotNull
    private Integer doiId;

    /** BanThang | TheVang | TheDo */
    @NotBlank
    private String loaiSuKien;

    @NotNull
    private Integer phut;

    private String ghiChu;
}
