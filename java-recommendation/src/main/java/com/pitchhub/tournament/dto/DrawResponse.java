package com.pitchhub.tournament.dto;

import java.util.ArrayList;
import java.util.List;

public class DrawResponse {
    private List<GroupAssignmentDto> assignments = new ArrayList<>();
    private List<String> warnings = new ArrayList<>();

    public List<GroupAssignmentDto> getAssignments() { return assignments; }
    public void setAssignments(List<GroupAssignmentDto> assignments) { this.assignments = assignments; }
    public List<String> getWarnings() { return warnings; }
    public void setWarnings(List<String> warnings) { this.warnings = warnings; }
}
