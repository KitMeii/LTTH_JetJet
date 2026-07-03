package com.pitchhub.tournament.dto.request;

import lombok.Getter;
import lombok.Setter;

/** Staff xac nhan chot ket qua tran — ghi chu tuy chon (VD: bien ban su co nho). */
@Getter
@Setter
public class KetThucTranRequest {
    private String ghiChu;
}
