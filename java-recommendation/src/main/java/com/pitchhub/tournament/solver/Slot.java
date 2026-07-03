package com.pitchhub.tournament.solver;

import java.time.LocalDate;
import java.util.Objects;

/**
 * 1 slot = (khungGioId, ngày). Dùng làm giá trị trong CSP.
 * equals/hashCode để dùng slot làm key trong Map/Set.
 */
public final class Slot {
    private final int khungGioId;
    private final LocalDate ngay;
    private final String gioBatDau;   // "HH:mm" — chỉ dùng cho log/warning
    private final String gioKetThuc;

    public Slot(int khungGioId, LocalDate ngay, String gioBatDau, String gioKetThuc) {
        this.khungGioId = khungGioId;
        this.ngay = ngay;
        this.gioBatDau = gioBatDau == null ? "" : gioBatDau;
        this.gioKetThuc = gioKetThuc == null ? "" : gioKetThuc;
    }

    public int getKhungGioId() { return khungGioId; }
    public LocalDate getNgay() { return ngay; }
    public String getGioBatDau() { return gioBatDau; }
    public String getGioKetThuc() { return gioKetThuc; }

    public boolean isWeekend() {
        int dow = ngay.getDayOfWeek().getValue(); // 1=Mon..7=Sun
        return dow == 6 || dow == 7;
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (!(o instanceof Slot other)) return false;
        return khungGioId == other.khungGioId && Objects.equals(ngay, other.ngay);
    }

    @Override
    public int hashCode() {
        return Objects.hash(khungGioId, ngay);
    }

    @Override
    public String toString() {
        return "Slot{kg=" + khungGioId + ", " + ngay + " " + gioBatDau + "-" + gioKetThuc + "}";
    }
}
