package com.pitchhub.checkin.controller;

import com.pitchhub.checkin.dto.CheckInRequest;
import com.pitchhub.checkin.dto.CheckInResult;
import com.pitchhub.checkin.service.CheckInService;
import com.pitchhub.checkin.util.QRCodeUtil;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/owner/checkin")
public class CheckInController {

    @Autowired
    private CheckInService checkInService;

    // GET /api/owner/checkin/lookup/{maXacNhan}?ownerId=123
    @GetMapping("/lookup/{maXacNhan}")
    public ResponseEntity<CheckInResult> lookup(
            @PathVariable("maXacNhan") String maXacNhan,
            @RequestParam(value = "ownerId", required = false) Integer ownerId) {
        CheckInResult res = checkInService.lookup(maXacNhan, ownerId);
        return ResponseEntity.ok(res);
    }

    // POST /api/owner/checkin/scan   body: { maXacNhan, ownerId }
    @PostMapping("/scan")
    public ResponseEntity<CheckInResult> scan(@RequestBody CheckInRequest req) {
        if (req == null || req.getMaXacNhan() == null || req.getMaXacNhan().isBlank()) {
            return ResponseEntity.badRequest().body(CheckInResult.fail("Thiếu mã xác nhận."));
        }
        CheckInResult res = checkInService.performCheckIn(req.getMaXacNhan(), req.getOwnerId());
        return ResponseEntity.ok(res);
    }

    // GET /api/owner/checkin/qr/{maXacNhan}?size=280
    @GetMapping(value = "/qr/{maXacNhan}", produces = MediaType.IMAGE_PNG_VALUE)
    public ResponseEntity<byte[]> qr(
            @PathVariable("maXacNhan") String maXacNhan,
            @RequestParam(value = "size", defaultValue = "280") int size) {
        try {
            int clamped = Math.max(120, Math.min(size, 800));
            byte[] png = QRCodeUtil.generatePng(maXacNhan, clamped);
            HttpHeaders headers = new HttpHeaders();
            headers.setContentType(MediaType.IMAGE_PNG);
            headers.setCacheControl("no-cache, no-store, must-revalidate");
            return new ResponseEntity<>(png, headers, HttpStatus.OK);
        } catch (Exception e) {
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                    .body(("QR error: " + e.getMessage()).getBytes());
        }
    }
}
