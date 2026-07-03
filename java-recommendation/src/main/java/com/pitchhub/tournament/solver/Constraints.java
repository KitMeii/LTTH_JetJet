package com.pitchhub.tournament.solver;

/**
 * Cấu hình ràng buộc cho solver.
 * - minRestDays: số ngày tối thiểu 1 đội nghỉ giữa 2 trận (0 = không ràng buộc)
 * - forbidSameDay: cấm 1 đội đá 2 trận cùng ngày
 * - preferWeekend: ưu tiên T7/CN (heuristic LCV, không phải hard)
 */
public final class Constraints {
    private int minRestDays = 1;
    private boolean forbidSameDay = true;
    private boolean preferWeekend = true;

    public int getMinRestDays() { return minRestDays; }
    public void setMinRestDays(int minRestDays) { this.minRestDays = Math.max(0, minRestDays); }

    public boolean isForbidSameDay() { return forbidSameDay; }
    public void setForbidSameDay(boolean forbidSameDay) { this.forbidSameDay = forbidSameDay; }

    public boolean isPreferWeekend() { return preferWeekend; }
    public void setPreferWeekend(boolean preferWeekend) { this.preferWeekend = preferWeekend; }
}
