package com.pitchhub.tournament.dto;

public class MatchDto {
    private int matchId;
    private int teamA;
    private int teamB;
    private String teamAName;
    private String teamBName;
    private int round;
    private int groupId;
    private String groupName;

    public int getMatchId() { return matchId; }
    public void setMatchId(int matchId) { this.matchId = matchId; }
    public int getTeamA() { return teamA; }
    public void setTeamA(int teamA) { this.teamA = teamA; }
    public int getTeamB() { return teamB; }
    public void setTeamB(int teamB) { this.teamB = teamB; }
    public String getTeamAName() { return teamAName; }
    public void setTeamAName(String teamAName) { this.teamAName = teamAName; }
    public String getTeamBName() { return teamBName; }
    public void setTeamBName(String teamBName) { this.teamBName = teamBName; }
    public int getRound() { return round; }
    public void setRound(int round) { this.round = round; }
    public int getGroupId() { return groupId; }
    public void setGroupId(int groupId) { this.groupId = groupId; }
    public String getGroupName() { return groupName; }
    public void setGroupName(String groupName) { this.groupName = groupName; }
}
