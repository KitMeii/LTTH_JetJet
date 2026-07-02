package com.pitchhub.tournament.solver;

/**
 * Biến CSP = 1 trận đấu.
 * matchId, 2 đội, vòng (round), bảng đấu (groupId).
 */
public final class Variable {
    private final int matchId;
    private final int teamA;
    private final int teamB;
    private final String teamAName;
    private final String teamBName;
    private final int round;
    private final int groupId;
    private final String groupName;

    public Variable(int matchId, int teamA, int teamB,
                    String teamAName, String teamBName,
                    int round, int groupId, String groupName) {
        this.matchId = matchId;
        this.teamA = teamA;
        this.teamB = teamB;
        this.teamAName = teamAName == null ? "" : teamAName;
        this.teamBName = teamBName == null ? "" : teamBName;
        this.round = round;
        this.groupId = groupId;
        this.groupName = groupName == null ? "" : groupName;
    }

    public int getMatchId() { return matchId; }
    public int getTeamA() { return teamA; }
    public int getTeamB() { return teamB; }
    public String getTeamAName() { return teamAName; }
    public String getTeamBName() { return teamBName; }
    public int getRound() { return round; }
    public int getGroupId() { return groupId; }
    public String getGroupName() { return groupName; }

    /** true nếu trận có 1 trong 2 đội trùng với trận `other`. */
    public boolean sharesTeamWith(Variable other) {
        return teamA == other.teamA || teamA == other.teamB
            || teamB == other.teamA || teamB == other.teamB;
    }

    public boolean hasTeam(int teamId) {
        return teamA == teamId || teamB == teamId;
    }

    @Override
    public String toString() {
        return "Match#" + matchId + "[" + teamAName + " vs " + teamBName
             + ", V." + round + ", B." + groupName + "]";
    }
}
