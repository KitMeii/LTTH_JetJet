package com.pitchhub.tournament.controller;

import com.pitchhub.tournament.dto.*;
import com.pitchhub.tournament.service.GroupDrawService;
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
    private final GroupDrawService drawService;

    @Autowired
    public TournamentSchedulerController(TournamentSchedulerService service,
                                         GroupDrawService drawService) {
        this.service = service;
        this.drawService = drawService;
    }

    @PostMapping("/schedule")
    public ResponseEntity<ScheduleResponse> schedule(@RequestBody ScheduleRequest req) {
        return ResponseEntity.ok(service.schedule(req));
    }

    @PostMapping("/validate")
    public ResponseEntity<ValidateResponse> validate(@RequestBody ValidateRequest req) {
        return ResponseEntity.ok(service.validate(req));
    }

    /**
     * Bốc thăm chia bảng — dùng trong luồng AutoMode ở .NET.
     * Trả về map teamId → groupIndex (0-based).
     */
    @PostMapping("/draw")
    public ResponseEntity<DrawResponse> draw(@RequestBody DrawRequest req) {
        return ResponseEntity.ok(drawService.draw(req));
    }
}
