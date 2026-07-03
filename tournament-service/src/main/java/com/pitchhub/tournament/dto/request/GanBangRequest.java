package com.pitchhub.tournament.dto.request;

import jakarta.validation.constraints.NotNull;
import lombok.Getter;
import lombok.Setter;

/** DTO bo sung — tuong duong GanDoiVaoBang(doiId, bangId) trong TournamentService.cs. */
@Getter
@Setter
public class GanBangRequest {
    @NotNull
    private Integer doiId;

    /** null = go doi ra khoi bang (chua xep). */
    private Integer bangId;
}
