package com.pitchhub.tournament.dto;

import java.util.ArrayList;
import java.util.List;

public class ScheduleRequest {
    private int giaiId;
    private List<MatchDto> matches = new ArrayList<>();
    private List<SlotDto> availableSlots = new ArrayList<>();
    private List<BookingConflictDto> bookings = new ArrayList<>();
    private ConstraintsDto constraints = new ConstraintsDto();

    public int getGiaiId() { return giaiId; }
    public void setGiaiId(int giaiId) { this.giaiId = giaiId; }
    public List<MatchDto> getMatches() { return matches; }
    public void setMatches(List<MatchDto> matches) { this.matches = matches; }
    public List<SlotDto> getAvailableSlots() { return availableSlots; }
    public void setAvailableSlots(List<SlotDto> availableSlots) { this.availableSlots = availableSlots; }
    public List<BookingConflictDto> getBookings() { return bookings; }
    public void setBookings(List<BookingConflictDto> bookings) { this.bookings = bookings; }
    public ConstraintsDto getConstraints() { return constraints; }
    public void setConstraints(ConstraintsDto constraints) { this.constraints = constraints; }
}
