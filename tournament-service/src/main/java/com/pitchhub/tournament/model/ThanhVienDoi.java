package com.pitchhub.tournament.model;

import jakarta.persistence.*;

import java.util.ArrayList;
import java.util.List;

/** Map bang ThanhVienDois (cau thu trong 1 doi bong). */
@Entity
@Table(name = "ThanhVienDois")
public class ThanhVienDoi {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Integer id;

    @Column(name = "DoiId", nullable = false, insertable = false, updatable = false)
    private Integer doiId;

    @Column(name = "HoTen", nullable = false)
    private String hoTen;

    @Column(name = "SoAo", nullable = false)
    private Integer soAo;

    @Column(name = "AnhDaiDien")
    private String anhDaiDien;

    @Column(name = "SoTranTreoGio", nullable = false)
    private Integer soTranTreoGio = 0;

    @Column(name = "TongBanThang", nullable = false)
    private Integer tongBanThang = 0;

    @Column(name = "TongTheVang", nullable = false)
    private Integer tongTheVang = 0;

    @Column(name = "TongTheDo", nullable = false)
    private Integer tongTheDo = 0;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "DoiId", nullable = false)
    private DoiBong doi;

    @OneToMany(mappedBy = "thanhVien")
    private List<SuKienTran> suKiens = new ArrayList<>();

    public ThanhVienDoi() {}

    public Integer getId() { return id; }
    public void setId(Integer id) { this.id = id; }

    public Integer getDoiId() { return doiId; }
    public void setDoiId(Integer doiId) { this.doiId = doiId; }

    public String getHoTen() { return hoTen; }
    public void setHoTen(String hoTen) { this.hoTen = hoTen; }

    public Integer getSoAo() { return soAo; }
    public void setSoAo(Integer soAo) { this.soAo = soAo; }

    public String getAnhDaiDien() { return anhDaiDien; }
    public void setAnhDaiDien(String anhDaiDien) { this.anhDaiDien = anhDaiDien; }

    public Integer getSoTranTreoGio() { return soTranTreoGio; }
    public void setSoTranTreoGio(Integer soTranTreoGio) { this.soTranTreoGio = soTranTreoGio; }

    public Integer getTongBanThang() { return tongBanThang; }
    public void setTongBanThang(Integer tongBanThang) { this.tongBanThang = tongBanThang; }

    public Integer getTongTheVang() { return tongTheVang; }
    public void setTongTheVang(Integer tongTheVang) { this.tongTheVang = tongTheVang; }

    public Integer getTongTheDo() { return tongTheDo; }
    public void setTongTheDo(Integer tongTheDo) { this.tongTheDo = tongTheDo; }

    public DoiBong getDoi() { return doi; }
    public void setDoi(DoiBong doi) { this.doi = doi; }

    public List<SuKienTran> getSuKiens() { return suKiens; }
    public void setSuKiens(List<SuKienTran> suKiens) { this.suKiens = suKiens; }
}
