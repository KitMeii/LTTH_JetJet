package com.pitchhub.tournament.dto.request;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Getter;
import lombok.Setter;

/**
 * DTO bo sung ngoai 5 request DTO liet ke ban dau — can thiet cho endpoint
 * "xu ly su co" (POST /tournament/owner/{id}/incident), tuong duong
 * XuLySuCo(tranDauId, doiBoCuocId, lyDo) trong TournamentController.cs.
 */
@Getter
@Setter
public class IncidentRequest {
    @NotNull
    private Integer tranDauId;

    @NotNull
    private Integer doiBoCuocId;

    @NotBlank(message = "Phai nhap ly do xu ly su co")
    private String lyDo;
}
