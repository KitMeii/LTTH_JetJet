package com.pitchhub.tournament.exception;

import org.springframework.http.HttpStatus;

public class TournamentException extends RuntimeException {

    public enum Code {
        GIAI_KHONG_TON_TAI(HttpStatus.NOT_FOUND),
        KHONG_CO_QUYEN(HttpStatus.FORBIDDEN),
        TRANG_THAI_KHONG_HOP_LE(HttpStatus.BAD_REQUEST),
        DOI_DA_THANH_TOAN(HttpStatus.BAD_REQUEST),
        GIA_KHONG_DU_DOI(HttpStatus.BAD_REQUEST);

        private final HttpStatus status;

        Code(HttpStatus status) {
            this.status = status;
        }

        public HttpStatus getStatus() {
            return status;
        }
    }

    private final Code code;

    public TournamentException(Code code, String message) {
        super(message);
        this.code = code;
    }

    public Code getCode() {
        return code;
    }
}
