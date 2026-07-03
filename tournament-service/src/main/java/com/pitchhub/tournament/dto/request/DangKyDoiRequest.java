package com.pitchhub.tournament.dto.request;

import jakarta.validation.constraints.NotBlank;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class DangKyDoiRequest {
    @NotBlank(message = "Ten doi khong duoc de trong")
    private String tenDoi;
}
