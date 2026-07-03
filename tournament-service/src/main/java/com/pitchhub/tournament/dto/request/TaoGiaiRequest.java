package com.pitchhub.tournament.dto.request;

import jakarta.validation.constraints.*;
import lombok.Getter;
import lombok.Setter;

import java.math.BigDecimal;
import java.time.LocalDateTime;

/** Tuong duong CreateGiaiDauDto trong TournamentService.cs. */
@Getter
@Setter
public class TaoGiaiRequest {

    @NotBlank(message = "Ten giai khong duoc de trong")
    private String tenGiai;

    private String moTa;

    @NotNull
    private Integer sanBongId;

    @NotNull
    private Integer soDoiToiDa = 8;

    @NotNull
    @Min(1)
    private Integer soBang = 2;

    @NotNull
    @DecimalMin(value = "0", inclusive = true)
    private BigDecimal lePhiGiai;

    @NotNull
    @DecimalMin(value = "0", inclusive = true)
    private BigDecimal tienKyQuy;

    private BigDecimal tienPhatTheVang = BigDecimal.valueOf(20000);
    private BigDecimal tienPhatTheDo = BigDecimal.valueOf(100000);
    private Integer soTranTreoGioTheDo = 1;
    private Integer soTheVangTichLuy = 2;

    @NotNull
    private LocalDateTime ngayBatDau;

    @NotNull
    private LocalDateTime ngayKetThuc;

    private LocalDateTime thoiGianDong;
}
