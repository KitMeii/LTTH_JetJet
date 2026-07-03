package com.pitchhub.tournament.dto;

public class SlotDto {
    private int khungGioId;
    private String ngay;        // yyyy-MM-dd
    private String gioBatDau;   // HH:mm
    private String gioKetThuc;  // HH:mm

    public int getKhungGioId() { return khungGioId; }
    public void setKhungGioId(int khungGioId) { this.khungGioId = khungGioId; }
    public String getNgay() { return ngay; }
    public void setNgay(String ngay) { this.ngay = ngay; }
    public String getGioBatDau() { return gioBatDau; }
    public void setGioBatDau(String gioBatDau) { this.gioBatDau = gioBatDau; }
    public String getGioKetThuc() { return gioKetThuc; }
    public void setGioKetThuc(String gioKetThuc) { this.gioKetThuc = gioKetThuc; }
}
