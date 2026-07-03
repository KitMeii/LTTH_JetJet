package com.pitchhub.tournament.dto;

public class ConstraintsDto {
    private int minRestDays = 1;
    private boolean forbidSameDay = true;
    private boolean preferWeekend = true;

    public int getMinRestDays() { return minRestDays; }
    public void setMinRestDays(int minRestDays) { this.minRestDays = minRestDays; }
    public boolean isForbidSameDay() { return forbidSameDay; }
    public void setForbidSameDay(boolean forbidSameDay) { this.forbidSameDay = forbidSameDay; }
    public boolean isPreferWeekend() { return preferWeekend; }
    public void setPreferWeekend(boolean preferWeekend) { this.preferWeekend = preferWeekend; }
}
