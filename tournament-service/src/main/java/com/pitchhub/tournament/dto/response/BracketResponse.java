package com.pitchhub.tournament.dto.response;

import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;
import lombok.AllArgsConstructor;

import java.util.List;

/** Cay bracket knock-out — moi vong (TuKet/BanKet/ChungKet) la 1 nhanh. */
@Getter
@Setter
@NoArgsConstructor
public class BracketResponse {
    private Integer giaiDauId;
    private List<Round> vongDau;

    public BracketResponse(Integer giaiDauId, List<Round> vongDau) {
        this.giaiDauId = giaiDauId;
        this.vongDau = vongDau;
    }

    @Getter
    @Setter
    @NoArgsConstructor
    @AllArgsConstructor
    public static class Round {
        private String loaiVong;
        private List<TranDauResponse> tranDaus;
    }
}
