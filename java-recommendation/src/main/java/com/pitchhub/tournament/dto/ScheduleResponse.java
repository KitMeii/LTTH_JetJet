package com.pitchhub.tournament.dto;

import java.util.ArrayList;
import java.util.List;

public class ScheduleResponse {
    private List<AssignmentDto> assignments = new ArrayList<>();
    private List<Integer> unassigned = new ArrayList<>();
    private List<String> warnings = new ArrayList<>();

    public List<AssignmentDto> getAssignments() { return assignments; }
    public void setAssignments(List<AssignmentDto> assignments) { this.assignments = assignments; }
    public List<Integer> getUnassigned() { return unassigned; }
    public void setUnassigned(List<Integer> unassigned) { this.unassigned = unassigned; }
    public List<String> getWarnings() { return warnings; }
    public void setWarnings(List<String> warnings) { this.warnings = warnings; }
}
