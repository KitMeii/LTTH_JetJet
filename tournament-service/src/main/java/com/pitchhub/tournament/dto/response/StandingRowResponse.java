package com.pitchhub.tournament.dto.response;

import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;

/** Tuong duong StandingRow trong StandingService.cs. */
@Getter
@Setter
@NoArgsConstructor
public class StandingRowResponse {
    private Integer doiId;
    private String tenDoi;
    private String logoUrl;
    private int thuHang;
    private int soTran;
    private int thang;
    private int hoa;
    private int thua;
    private int banThang;
    private int banThua;
    private int diem;

    public int getHieuSo() {
        return banThang - banThua;
    }
}
