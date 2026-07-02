package com.pitchhub.tournament.dto;

public class ValidateResponse {
    private boolean ok;
    private String reason;

    public ValidateResponse() {}
    public ValidateResponse(boolean ok, String reason) {
        this.ok = ok;
        this.reason = reason;
    }

    public boolean isOk() { return ok; }
    public void setOk(boolean ok) { this.ok = ok; }
    public String getReason() { return reason; }
    public void setReason(String reason) { this.reason = reason; }
}
