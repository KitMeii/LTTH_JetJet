package com.pitchhub.recommendation.controller;

import com.pitchhub.recommendation.service.PricingSuggestionService;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.web.bind.annotation.*;
import java.util.Map;

@RestController
@RequestMapping("/api/owner")
@CrossOrigin(origins = "*")
public class SuggestionController {

    @Autowired
    private PricingSuggestionService suggestionService;

    @PostMapping("/suggest-price")
    public Map<String, Object> suggestPrice(@RequestBody Map<String, Object> payload) {
        Integer sanId = (Integer) payload.get("sanId");
        Integer month = (Integer) payload.get("month");
        Integer year = (Integer) payload.get("year");

        var result = suggestionService.suggestPrice(sanId, month, year);

        return Map.of(
            "suggestedGoldPrice", result.suggestedGoldPrice(),
            "suggestedRegularPrice", result.suggestedRegularPrice(),
            "reason", result.reason(),
            "confidence", result.confidence()
        );
    }
}