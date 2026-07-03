package com.pitchhub.tournament.dto.request;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Getter;
import lombok.Setter;

/**
 * Them cau thu vao doi. KHONG xu ly upload file nhu CloudinaryService ben C#
 * (ngoai pham vi backend giai dau thuan REST) — anhDaiDien nhan URL co san
 * do client tu upload roi truyen vao (neu co).
 */
@Getter
@Setter
public class ThemThanhVienRequest {
    @NotBlank(message = "Ten cau thu khong duoc de trong")
    private String hoTen;

    @NotNull
    @Min(0)
    private Integer soAo;

    private String anhDaiDien;
}
