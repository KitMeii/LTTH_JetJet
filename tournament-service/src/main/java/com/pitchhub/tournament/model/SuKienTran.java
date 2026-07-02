package com.pitchhub.tournament.model;

import jakarta.persistence.*;

import java.time.LocalDateTime;

/** Map bang SuKienTrans (su kien trong tran: ban thang / the vang / the do / su co). */
@Entity
@Table(name = "SuKienTrans")
public class SuKienTran {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Integer id;

    @Column(name = "TranDauId", nullable = false, insertable = false, updatable = false)
    private Integer tranDauId;

    @Column(name = "ThanhVienId", insertable = false, updatable = false)
    private Integer thanhVienId;

    @Column(name = "DoiId", insertable = false, updatable = false)
    private Integer doiId;

    /** BanThang | TheVang | TheDo | TheVangLan2 | SuCo */
    @Column(name = "LoaiSuKien", nullable = false)
    private String loaiSuKien;

    @Column(name = "Phut")
    private Integer phut;

    @Column(name = "GhiChu")
    private String ghiChu;

    @Column(name = "ThoiGianGhi", nullable = false)
    private LocalDateTime thoiGianGhi;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "TranDauId", nullable = false)
    private TranDau tranDau;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "ThanhVienId")
    private ThanhVienDoi thanhVien;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "DoiId")
    private DoiBong doi;

    public SuKienTran() {}

    public Integer getId() { return id; }
    public void setId(Integer id) { this.id = id; }

    public Integer getTranDauId() { return tranDauId; }
    public void setTranDauId(Integer tranDauId) { this.tranDauId = tranDauId; }

    public Integer getThanhVienId() { return thanhVienId; }
    public void setThanhVienId(Integer thanhVienId) { this.thanhVienId = thanhVienId; }

    public Integer getDoiId() { return doiId; }
    public void setDoiId(Integer doiId) { this.doiId = doiId; }

    public String getLoaiSuKien() { return loaiSuKien; }
    public void setLoaiSuKien(String loaiSuKien) { this.loaiSuKien = loaiSuKien; }

    public Integer getPhut() { return phut; }
    public void setPhut(Integer phut) { this.phut = phut; }

    public String getGhiChu() { return ghiChu; }
    public void setGhiChu(String ghiChu) { this.ghiChu = ghiChu; }

    public LocalDateTime getThoiGianGhi() { return thoiGianGhi; }
    public void setThoiGianGhi(LocalDateTime thoiGianGhi) { this.thoiGianGhi = thoiGianGhi; }

    public TranDau getTranDau() { return tranDau; }
    public void setTranDau(TranDau tranDau) { this.tranDau = tranDau; }

    public ThanhVienDoi getThanhVien() { return thanhVien; }
    public void setThanhVien(ThanhVienDoi thanhVien) { this.thanhVien = thanhVien; }

    public DoiBong getDoi() { return doi; }
    public void setDoi(DoiBong doi) { this.doi = doi; }
}
