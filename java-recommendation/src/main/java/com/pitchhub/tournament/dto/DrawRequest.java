package com.pitchhub.tournament.dto;

import java.util.ArrayList;
import java.util.List;

/**
 * Yêu cầu bốc thăm chia bảng.
 * giaiId dùng làm seed cho Random để retry idempotent.
 */
public class DrawRequest {
    private int giaiId;
    private int soBang;
    private List<TeamDto> teams = new ArrayList<>();

    public int getGiaiId() { return giaiId; }
    public void setGiaiId(int giaiId) { this.giaiId = giaiId; }
    public int getSoBang() { return soBang; }
    public void setSoBang(int soBang) { this.soBang = soBang; }
    public List<TeamDto> getTeams() { return teams; }
    public void setTeams(List<TeamDto> teams) { this.teams = teams; }
}
