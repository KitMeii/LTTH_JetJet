package com.pitchhub.tournament.dto;

public class AssignmentDto {
    private int matchId;
    private int khungGioId;
    private String ngay; // yyyy-MM-dd

    public AssignmentDto() {}

    public AssignmentDto(int matchId, int khungGioId, String ngay) {
        this.matchId = matchId;
        this.khungGioId = khungGioId;
        this.ngay = ngay;
    }

    public int getMatchId() { return matchId; }
    public void setMatchId(int matchId) { this.matchId = matchId; }
    public int getKhungGioId() { return khungGioId; }
    public void setKhungGioId(int khungGioId) { this.khungGioId = khungGioId; }
    public String getNgay() { return ngay; }
    public void setNgay(String ngay) { this.ngay = ngay; }
}
