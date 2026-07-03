package com.pitchhub.report.dto;

public class ReportDetail {
    private String label;
    private double totalRevenue;
    private int bookings;
    private double netRevenue;

    // Getters and Setters
    public String getLabel() { return label; }
    public void setLabel(String label) { this.label = label; }
    public double getTotalRevenue() { return totalRevenue; }
    public void setTotalRevenue(double totalRevenue) { this.totalRevenue = totalRevenue; }
    public int getBookings() { return bookings; }
    public void setBookings(int bookings) { this.bookings = bookings; }
    public double getNetRevenue() { return netRevenue; }
    public void setNetRevenue(double netRevenue) { this.netRevenue = netRevenue; }
}