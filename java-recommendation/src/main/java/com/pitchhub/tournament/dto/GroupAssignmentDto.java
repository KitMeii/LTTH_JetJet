package com.pitchhub.tournament.dto;

/**
 * Kết quả bốc thăm: mỗi đội thuộc bảng nào (groupIndex 0-based).
 * .NET sẽ map groupIndex → BangDaus.OrderBy(Id).Skip(groupIndex).First().Id
 */
public class GroupAssignmentDto {
    private int teamId;
    private int groupIndex;

    public GroupAssignmentDto() {}

    public GroupAssignmentDto(int teamId, int groupIndex) {
        this.teamId = teamId;
        this.groupIndex = groupIndex;
    }

    public int getTeamId() { return teamId; }
    public void setTeamId(int teamId) { this.teamId = teamId; }
    public int getGroupIndex() { return groupIndex; }
    public void setGroupIndex(int groupIndex) { this.groupIndex = groupIndex; }
}
