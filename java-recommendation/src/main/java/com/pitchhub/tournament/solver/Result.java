package com.pitchhub.tournament.solver;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Kết quả solver: assignments + list biến không xếp được + warnings.
 */
public final class Result {
    private final Map<Integer, Slot> assignments = new HashMap<>();
    private final List<Integer> unassigned = new ArrayList<>();
    private final List<String> warnings = new ArrayList<>();

    public Map<Integer, Slot> getAssignments() { return assignments; }
    public List<Integer> getUnassigned() { return unassigned; }
    public List<String> getWarnings() { return warnings; }
}
