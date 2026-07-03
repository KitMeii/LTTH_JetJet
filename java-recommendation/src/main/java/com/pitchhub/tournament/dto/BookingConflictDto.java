package com.pitchhub.tournament.dto;

public class BookingConflictDto {
    private int khungGioId;
    private String ngay; // yyyy-MM-dd

    public int getKhungGioId() { return khungGioId; }
    public void setKhungGioId(int khungGioId) { this.khungGioId = khungGioId; }
    public String getNgay() { return ngay; }
    public void setNgay(String ngay) { this.ngay = ngay; }
}
