package com.pitchhub.tournament.dto.response;

import java.math.BigDecimal;
import java.util.ArrayList;
import java.util.List;

/** Tuong duong du lieu ViewBag trong AdminTournamentController.BaoCao() ben C#. */
public class BaoCaoResponse {
    public List<ThangKe> bieu6Thang = new ArrayList<>();
    public List<TopOwnerRow> topOwner = new ArrayList<>();
    public long tongGiaiDau;
    public long giaiHoanThanh;
    public long tongDoi;
    public long tongTranDau;
    public BigDecimal tongDoanhThu;

    public static class ThangKe {
        public String thang;
        public long soGiai;
        public long soDoiThanhToan;
        public BigDecimal doanhThu;
    }

    public static class TopOwnerRow {
        public String hoTen;
        public String email;
        public long soGiai;
        public long soActive;
    }
}
