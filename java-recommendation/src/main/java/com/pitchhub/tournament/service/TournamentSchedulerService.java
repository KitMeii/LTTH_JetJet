package com.pitchhub.tournament.service;

import com.pitchhub.tournament.dto.*;
import com.pitchhub.tournament.solver.Constraints;
import com.pitchhub.tournament.solver.CspSolver;
import com.pitchhub.tournament.solver.Slot;
import com.pitchhub.tournament.solver.Variable;
import org.springframework.stereotype.Service;

import java.time.LocalDate;
import java.util.*;

/**
 * Orchestrator: nhận DTO từ .NET → build biến/miền cho CspSolver → map kết quả.
 */
@Service
public class TournamentSchedulerService {

    /* ─────────────── /schedule ─────────────── */

    public ScheduleResponse schedule(ScheduleRequest req) {
        List<Variable> vars = req.getMatches().stream()
                .map(this::toVariable).toList();

        List<Slot> slots = req.getAvailableSlots().stream()
                .map(this::toSlot).toList();

        Set<Slot> blocked = new HashSet<>();
        for (BookingConflictDto b : req.getBookings()) {
            LocalDate d = LocalDate.parse(b.getNgay());
            blocked.add(new Slot(b.getKhungGioId(), d, "", ""));
        }

        Constraints c = toConstraints(req.getConstraints());

        CspSolver solver = new CspSolver();
        var result = solver.solve(vars, slots, blocked, c);

        ScheduleResponse out = new ScheduleResponse();
        for (Map.Entry<Integer, Slot> e : result.getAssignments().entrySet()) {
            out.getAssignments().add(new AssignmentDto(
                    e.getKey(),
                    e.getValue().getKhungGioId(),
                    e.getValue().getNgay().toString()   // yyyy-MM-dd
            ));
        }
        out.getUnassigned().addAll(result.getUnassigned());
        out.getWarnings().addAll(result.getWarnings());
        return out;
    }

    /* ─────────────── /validate ─────────────── */

    public ValidateResponse validate(ValidateRequest req) {
        Variable v = new Variable(
                req.getMatchId(), req.getTeamA(), req.getTeamB(),
                findName(req.getAllMatches(), req.getMatchId(), true),
                findName(req.getAllMatches(), req.getMatchId(), false),
                0, 0, ""
        );
        Slot target = new Slot(req.getKhungGioId(), LocalDate.parse(req.getNgay()), "", "");

        List<Variable> all = req.getAllMatches().stream().map(this::toVariable).toList();

        Map<Integer, Slot> existing = new HashMap<>();
        for (AssignmentDto a : req.getExistingAssignments()) {
            existing.put(a.getMatchId(),
                    new Slot(a.getKhungGioId(), LocalDate.parse(a.getNgay()), "", ""));
        }

        Set<Slot> blocked = new HashSet<>();
        for (BookingConflictDto b : req.getBookings()) {
            blocked.add(new Slot(b.getKhungGioId(), LocalDate.parse(b.getNgay()), "", ""));
        }

        Constraints c = toConstraints(req.getConstraints());

        CspSolver solver = new CspSolver();
        String reason = solver.validateMove(v, target, all, existing, blocked, c);
        return reason == null
                ? new ValidateResponse(true, null)
                : new ValidateResponse(false, reason);
    }

    /* ─────────────── mappers ─────────────── */

    private Variable toVariable(MatchDto m) {
        return new Variable(
                m.getMatchId(), m.getTeamA(), m.getTeamB(),
                m.getTeamAName(), m.getTeamBName(),
                m.getRound(), m.getGroupId(), m.getGroupName()
        );
    }

    private Slot toSlot(SlotDto s) {
        return new Slot(
                s.getKhungGioId(),
                LocalDate.parse(s.getNgay()),
                s.getGioBatDau(),
                s.getGioKetThuc()
        );
    }

    private Constraints toConstraints(ConstraintsDto d) {
        Constraints c = new Constraints();
        if (d != null) {
            c.setMinRestDays(d.getMinRestDays());
            c.setForbidSameDay(d.isForbidSameDay());
            c.setPreferWeekend(d.isPreferWeekend());
        }
        return c;
    }

    private String findName(List<MatchDto> all, int matchId, boolean teamA) {
        for (MatchDto m : all) {
            if (m.getMatchId() == matchId) {
                return teamA ? m.getTeamAName() : m.getTeamBName();
            }
        }
        return "?";
    }
}
