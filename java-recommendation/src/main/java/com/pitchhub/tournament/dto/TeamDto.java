package com.pitchhub.tournament.dto;

/**
 * Đội tham gia bốc thăm chia bảng.
 * seedRating: điểm hạt giống (0 = không có → shuffle thuần).
 */
public class TeamDto {
    private int id;
    private String tenDoi;
    private int seedRating;

    public int getId() { return id; }
    public void setId(int id) { this.id = id; }
    public String getTenDoi() { return tenDoi; }
    public void setTenDoi(String tenDoi) { this.tenDoi = tenDoi; }
    public int getSeedRating() { return seedRating; }
    public void setSeedRating(int seedRating) { this.seedRating = seedRating; }
}
