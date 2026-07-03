package com.pitchhub.tournament.controller;

import com.pitchhub.tournament.dto.*;
import com.pitchhub.tournament.service.TournamentSchedulerService;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

/**
 * HTTP endpoint cho tính năng "Xếp lịch giải đấu tối ưu".
 * Được gọi từ Web_Stadium (.NET) qua TournamentSchedulerClient.
 */
@RestController
@RequestMapping("/api/tournament")
public class TournamentSchedulerController {

    private final TournamentSchedulerService service;

    @Autowired
    public TournamentSchedulerController(TournamentSchedulerService service) {
        this.service = service;
    }

    @PostMapping("/schedule")
    public ResponseEntity<ScheduleResponse> schedule(@RequestBody ScheduleRequest req) {
        return ResponseEntity.ok(service.schedule(req));
    }

    @PostMapping("/validate")
    public ResponseEntity<ValidateResponse> validate(@RequestBody ValidateRequest req) {
        return ResponseEntity.ok(service.validate(req));
    }
}
