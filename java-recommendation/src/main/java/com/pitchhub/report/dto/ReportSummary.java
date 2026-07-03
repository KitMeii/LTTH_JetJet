package com.pitchhub.report.dto;

public class ReportSummary {
    private double totalRevenue;
    private int totalBookings;
    private double avgPerBooking;
    private double commission;
    private double netRevenue;

    // Getters and Setters
    public double getTotalRevenue() { return totalRevenue; }
    public void setTotalRevenue(double totalRevenue) { this.totalRevenue = totalRevenue; }
    public int getTotalBookings() { return totalBookings; }
    public void setTotalBookings(int totalBookings) { this.totalBookings = totalBookings; }
    public double getAvgPerBooking() { return avgPerBooking; }
    public void setAvgPerBooking(double avgPerBooking) { this.avgPerBooking = avgPerBooking; }
    public double getCommission() { return commission; }
    public void setCommission(double commission) { this.commission = commission; }
    public double getNetRevenue() { return netRevenue; }
    public void setNetRevenue(double netRevenue) { this.netRevenue = netRevenue; }
}