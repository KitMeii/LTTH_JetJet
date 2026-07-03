package com.pitchhub.report.dto;

import java.util.List;

public class ReportData {
    private String title;
    private String stadiumName;
    private String period;
    private ReportSummary summary;
    private List<ReportDetail> details;

    // Getters and Setters
    public String getTitle() { return title; }
    public void setTitle(String title) { this.title = title; }
    public String getStadiumName() { return stadiumName; }
    public void setStadiumName(String stadiumName) { this.stadiumName = stadiumName; }
    public String getPeriod() { return period; }
    public void setPeriod(String period) { this.period = period; }
    public ReportSummary getSummary() { return summary; }
    public void setSummary(ReportSummary summary) { this.summary = summary; }
    public List<ReportDetail> getDetails() { return details; }
    public void setDetails(List<ReportDetail> details) { this.details = details; }
}