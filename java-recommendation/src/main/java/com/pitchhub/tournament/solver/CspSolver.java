package com.pitchhub.tournament.solver;

import java.time.LocalDate;
import java.time.temporal.ChronoUnit;
import java.util.*;

/**
 * CSP solver cho bài toán xếp lịch giải đấu.
 *
 *  Biến    (Variable) = trận đấu
 *  Miền    (Domain)   = list slot khả dụng, không bị blocked (booking thường / dummy)
 *  Ràng buộc cứng:
 *    (C1) 1 slot ↔ tối đa 1 trận
 *    (C2) 1 đội không đá 2 trận cùng ngày (nếu forbidSameDay)
 *    (C3) Nghỉ tối thiểu N ngày giữa 2 trận của cùng 1 đội (minRestDays)
 *  Heuristic:
 *    - MRV: chọn biến chưa gán có domain nhỏ nhất
 *    - LCV: thứ tự thử slot ưu tiên (a) T7/CN nếu preferWeekend, (b) ngày sớm hơn
 *    - Forward-checking: sau mỗi gán, prune domain biến chưa gán
 *
 *  Fallback: nếu backtracking không giải trọn (search space quá chật),
 *  giữ những assignment đã tìm được ở nhánh sâu nhất, phần còn lại → unassigned.
 *
 *  Tham chiếu: Russell &amp; Norvig, "Artificial Intelligence: A Modern Approach",
 *  Chapter 6 — Constraint Satisfaction Problems.
 */
public final class CspSolver {

    private static final int NODE_LIMIT = 200_000;   // chặn worst case, không treo request

    /** Snapshot assignment sâu nhất tìm được — dùng cho best-partial. */
    private Map<Integer, Slot> bestPartial = new HashMap<>();
    private int nodeCount = 0;

    public Result solve(List<Variable> vars, List<Slot> slots, Set<Slot> blocked, Constraints c) {
        Objects.requireNonNull(vars);
        Objects.requireNonNull(slots);
        if (blocked == null) blocked = Collections.emptySet();
        if (c == null) c = new Constraints();

        Result result = new Result();

        // Domain ban đầu = tất cả slot không bị blocked
        Map<Integer, List<Slot>> domains = new HashMap<>();
        for (Variable v : vars) {
            List<Slot> dom = new ArrayList<>();
            for (Slot s : slots) if (!blocked.contains(s)) dom.add(s);
            domains.put(v.getMatchId(), dom);
        }

        Map<Integer, Variable> byId = new HashMap<>();
        for (Variable v : vars) byId.put(v.getMatchId(), v);

        bestPartial = new HashMap<>();
        nodeCount = 0;

        Map<Integer, Slot> assignment = new HashMap<>();
        boolean full = backtrack(assignment, byId, domains, c);

        // Nếu full, dùng chính assignment; nếu không, dùng snapshot sâu nhất
        Map<Integer, Slot> finalAssign = full ? assignment : bestPartial;

        for (Variable v : vars) {
            if (finalAssign.containsKey(v.getMatchId())) {
                result.getAssignments().put(v.getMatchId(), finalAssign.get(v.getMatchId()));
            } else {
                result.getUnassigned().add(v.getMatchId());
            }
        }

        // Warnings
        if (!result.getUnassigned().isEmpty()) {
            result.getWarnings().add("Còn " + result.getUnassigned().size()
                + " trận chưa xếp được — thiếu slot khả dụng thoả ràng buộc.");
        }
        addSoftWarnings(vars, result, c);

        return result;
    }

    // ─────────────────────── BACKTRACKING ───────────────────────

    private boolean backtrack(Map<Integer, Slot> assignment,
                              Map<Integer, Variable> byId,
                              Map<Integer, List<Slot>> domains,
                              Constraints c) {
        if (++nodeCount > NODE_LIMIT) return false;

        if (assignment.size() > bestPartial.size()) {
            bestPartial = new HashMap<>(assignment);
        }
        if (assignment.size() == byId.size()) return true;

        // MRV: chọn biến chưa gán có domain nhỏ nhất
        Variable next = selectMRV(assignment, byId, domains);
        if (next == null) return true;

        List<Slot> ordered = orderLCV(next, domains.get(next.getMatchId()), c);
        for (Slot slot : ordered) {
            if (!isConsistent(next, slot, assignment, byId, c)) continue;

            assignment.put(next.getMatchId(), slot);

            // Forward-checking: prune domain của biến chưa gán
            Map<Integer, List<Slot>> snapshot = snapshot(domains);
            forwardCheck(next, slot, assignment, byId, domains, c);

            if (!hasEmptyDomain(assignment, domains)) {
                if (backtrack(assignment, byId, domains, c)) return true;
            }

            // Undo
            assignment.remove(next.getMatchId());
            domains.clear();
            domains.putAll(snapshot);
        }
        return false;
    }

    private Variable selectMRV(Map<Integer, Slot> assignment,
                               Map<Integer, Variable> byId,
                               Map<Integer, List<Slot>> domains) {
        Variable pick = null;
        int minSize = Integer.MAX_VALUE;
        for (Variable v : byId.values()) {
            if (assignment.containsKey(v.getMatchId())) continue;
            int size = domains.get(v.getMatchId()).size();
            if (size < minSize) {
                minSize = size;
                pick = v;
            }
        }
        return pick;
    }

    private List<Slot> orderLCV(Variable v, List<Slot> domain, Constraints c) {
        // 1) preferWeekend đưa T7/CN lên đầu
        // 2) ngày sớm hơn ưu tiên (giữ tính "vòng đấu tăng dần theo thời gian")
        List<Slot> sorted = new ArrayList<>(domain);
        sorted.sort((a, b) -> {
            if (c.isPreferWeekend()) {
                int wa = a.isWeekend() ? 0 : 1;
                int wb = b.isWeekend() ? 0 : 1;
                if (wa != wb) return Integer.compare(wa, wb);
            }
            int cmp = a.getNgay().compareTo(b.getNgay());
            if (cmp != 0) return cmp;
            return Integer.compare(a.getKhungGioId(), b.getKhungGioId());
        });
        return sorted;
    }

    // ─────────────────────── CONSTRAINT CHECK ───────────────────────

    private boolean isConsistent(Variable v, Slot slot,
                                 Map<Integer, Slot> assignment,
                                 Map<Integer, Variable> byId,
                                 Constraints c) {
        for (Map.Entry<Integer, Slot> e : assignment.entrySet()) {
            Slot other = e.getValue();
            Variable ov = byId.get(e.getKey());

            // C1: slot uniqueness
            if (slot.equals(other)) return false;

            // C2/C3: chung team → check ngày
            if (!v.sharesTeamWith(ov)) continue;

            long days = Math.abs(ChronoUnit.DAYS.between(slot.getNgay(), other.getNgay()));
            if (c.isForbidSameDay() && days == 0) return false;
            if (c.getMinRestDays() > 0 && days < c.getMinRestDays()) return false;
        }
        return true;
    }

    private void forwardCheck(Variable v, Slot slot,
                              Map<Integer, Slot> assignment,
                              Map<Integer, Variable> byId,
                              Map<Integer, List<Slot>> domains,
                              Constraints c) {
        for (Variable ov : byId.values()) {
            if (assignment.containsKey(ov.getMatchId())) continue;

            List<Slot> dom = domains.get(ov.getMatchId());
            List<Slot> pruned = new ArrayList<>(dom.size());
            for (Slot s : dom) {
                if (s.equals(slot)) continue;       // C1
                if (!v.sharesTeamWith(ov)) {
                    pruned.add(s);
                    continue;
                }
                long days = Math.abs(ChronoUnit.DAYS.between(s.getNgay(), slot.getNgay()));
                if (c.isForbidSameDay() && days == 0) continue;
                if (c.getMinRestDays() > 0 && days < c.getMinRestDays()) continue;
                pruned.add(s);
            }
            domains.put(ov.getMatchId(), pruned);
        }
    }

    private boolean hasEmptyDomain(Map<Integer, Slot> assignment,
                                   Map<Integer, List<Slot>> domains) {
        for (Map.Entry<Integer, List<Slot>> e : domains.entrySet()) {
            if (assignment.containsKey(e.getKey())) continue;
            if (e.getValue().isEmpty()) return true;
        }
        return false;
    }

    private Map<Integer, List<Slot>> snapshot(Map<Integer, List<Slot>> domains) {
        Map<Integer, List<Slot>> copy = new HashMap<>();
        for (Map.Entry<Integer, List<Slot>> e : domains.entrySet()) {
            copy.put(e.getKey(), new ArrayList<>(e.getValue()));
        }
        return copy;
    }

    // ─────────────────────── SOFT WARNINGS ───────────────────────

    private void addSoftWarnings(List<Variable> vars, Result r, Constraints c) {
        // Tìm cặp trận cùng đội có khoảng cách sát ngưỡng minRestDays
        Map<Integer, Slot> assign = r.getAssignments();
        for (int i = 0; i < vars.size(); i++) {
            Variable vi = vars.get(i);
            Slot si = assign.get(vi.getMatchId());
            if (si == null) continue;
            for (int j = i + 1; j < vars.size(); j++) {
                Variable vj = vars.get(j);
                Slot sj = assign.get(vj.getMatchId());
                if (sj == null) continue;
                if (!vi.sharesTeamWith(vj)) continue;
                long days = Math.abs(ChronoUnit.DAYS.between(si.getNgay(), sj.getNgay()));
                if (days >= c.getMinRestDays() && days <= c.getMinRestDays() + 1) {
                    int sharedTeam = vi.getTeamA() == vj.getTeamA() || vi.getTeamA() == vj.getTeamB()
                                     ? vi.getTeamA() : vi.getTeamB();
                    String name = vi.hasTeam(sharedTeam)
                        ? (vi.getTeamA() == sharedTeam ? vi.getTeamAName() : vi.getTeamBName())
                        : "?";
                    r.getWarnings().add("Đội " + name + " có 2 trận cách nhau " + days + " ngày.");
                }
            }
        }
    }

    // ─────────────────────── VALIDATE 1 nước đi (nhẹ) ───────────────────────

    /**
     * Check 1 nước kéo thả: đặt trận `v` vào `target`, giữ nguyên các assignment còn lại.
     * Trả về lý do vi phạm, hoặc null nếu OK.
     */
    public String validateMove(Variable v, Slot target,
                               List<Variable> allMatches,
                               Map<Integer, Slot> existing,
                               Set<Slot> blocked,
                               Constraints c) {
        if (blocked != null && blocked.contains(target)) {
            return "Slot đã bị booking thường / đơn khác.";
        }
        Map<Integer, Variable> byId = new HashMap<>();
        for (Variable x : allMatches) byId.put(x.getMatchId(), x);

        for (Map.Entry<Integer, Slot> e : existing.entrySet()) {
            if (e.getKey() == v.getMatchId()) continue; // bỏ chính nó (đang di chuyển)
            Slot other = e.getValue();
            Variable ov = byId.get(e.getKey());
            if (ov == null) continue;

            if (target.equals(other)) {
                return "Slot đã có trận khác.";
            }
            if (!v.sharesTeamWith(ov)) continue;

            long days = Math.abs(ChronoUnit.DAYS.between(target.getNgay(), other.getNgay()));
            if (c.isForbidSameDay() && days == 0) {
                return sharedTeamName(v, ov) + " đã có trận trong ngày " + target.getNgay() + ".";
            }
            if (c.getMinRestDays() > 0 && days < c.getMinRestDays()) {
                return sharedTeamName(v, ov) + " cần nghỉ tối thiểu "
                     + c.getMinRestDays() + " ngày (hiện chỉ " + days + " ngày).";
            }
        }
        return null;
    }

    private String sharedTeamName(Variable a, Variable b) {
        if (a.getTeamA() == b.getTeamA() || a.getTeamA() == b.getTeamB()) return a.getTeamAName();
        return a.getTeamBName();
    }
}
