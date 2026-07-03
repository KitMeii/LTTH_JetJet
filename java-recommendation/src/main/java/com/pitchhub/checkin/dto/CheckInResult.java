package com.pitchhub.checkin.dto;

public class CheckInResult {
    private boolean success;
    private String message;
    private BookingInfo booking;

    public CheckInResult() {}

    public CheckInResult(boolean success, String message, BookingInfo booking) {
        this.success = success;
        this.message = message;
        this.booking = booking;
    }

    public static CheckInResult ok(String message, BookingInfo booking) {
        return new CheckInResult(true, message, booking);
    }

    public static CheckInResult fail(String message) {
        return new CheckInResult(false, message, null);
    }

    public static CheckInResult fail(String message, BookingInfo booking) {
        return new CheckInResult(false, message, booking);
    }

    public boolean isSuccess() { return success; }
    public void setSuccess(boolean success) { this.success = success; }
    public String getMessage() { return message; }
    public void setMessage(String message) { this.message = message; }
    public BookingInfo getBooking() { return booking; }
    public void setBooking(BookingInfo booking) { this.booking = booking; }
}
