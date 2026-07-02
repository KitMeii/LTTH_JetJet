package com.pitchhub.tournament.dto;

import java.util.ArrayList;
import java.util.List;

public class ValidateRequest {
    private int matchId;
    private int teamA;
    private int teamB;
    private int khungGioId;
    private String ngay; // yyyy-MM-dd

    private List<AssignmentDto> existingAssignments = new ArrayList<>();
    private List<BookingConflictDto> bookings = new ArrayList<>();
    private List<MatchDto> allMatches = new ArrayList<>();
    private ConstraintsDto constraints = new ConstraintsDto();

    public int getMatchId() { return matchId; }
    public void setMatchId(int matchId) { this.matchId = matchId; }
    public int getTeamA() { return teamA; }
    public void setTeamA(int teamA) { this.teamA = teamA; }
    public int getTeamB() { return teamB; }
    public void setTeamB(int teamB) { this.teamB = teamB; }
    public int getKhungGioId() { return khungGioId; }
    public void setKhungGioId(int khungGioId) { this.khungGioId = khungGioId; }
    public String getNgay() { return ngay; }
    public void setNgay(String ngay) { this.ngay = ngay; }
    public List<AssignmentDto> getExistingAssignments() { return existingAssignments; }
    public void setExistingAssignments(List<AssignmentDto> existingAssignments) { this.existingAssignments = existingAssignments; }
    public List<BookingConflictDto> getBookings() { return bookings; }
    public void setBookings(List<BookingConflictDto> bookings) { this.bookings = bookings; }
    public List<MatchDto> getAllMatches() { return allMatches; }
    public void setAllMatches(List<MatchDto> allMatches) { this.allMatches = allMatches; }
    public ConstraintsDto getConstraints() { return constraints; }
    public void setConstraints(ConstraintsDto constraints) { this.constraints = constraints; }
}
