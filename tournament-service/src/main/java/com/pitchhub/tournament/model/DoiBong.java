package com.pitchhub.tournament.model;

import jakarta.persistence.*;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

/** Map bang DoiBongs (doi bong dang ky thi dau trong 1 giai). */
@Entity
@Table(name = "DoiBongs")
public class DoiBong {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Integer id;

    @Column(name = "GiaiDauId", nullable = false, insertable = false, updatable = false)
    private Integer giaiDauId;

    @Column(name = "BangId", insertable = false, updatable = false)
    private Integer bangId;

    @Column(name = "DoiTruongId", nullable = false)
    private Integer doiTruongId;

    @Column(name = "TenDoi", nullable = false)
    private String tenDoi;

    @Column(name = "LogoUrl")
    private String logoUrl;

    @Column(name = "TienKyQuyConLai", nullable = false, precision = 18, scale = 2)
    private BigDecimal tienKyQuyConLai;

    @Column(name = "DaThanhToan", nullable = false)
    private Boolean daThanhToan = false;

    @Column(name = "ThoiGianThanhToan")
    private LocalDateTime thoiGianThanhToan;

    @Column(name = "TrangThai", nullable = false)
    private String trangThai = "Active";

    @Column(name = "ThoiGianTao", nullable = false)
    private LocalDateTime thoiGianTao;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "GiaiDauId", nullable = false)
    private GiaiDau giaiDau;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "BangId")
    private BangDau bang;

    // Set — cung ly do voi GiaiDau.doiBongs (tranh MultipleBagFetchException khi
    // @EntityGraph fetch dong thoi "doiBongs" + "doiBongs.thanhViens").
    @OneToMany(mappedBy = "doi", cascade = CascadeType.ALL, orphanRemoval = true)
    private Set<ThanhVienDoi> thanhViens = new LinkedHashSet<>();

    @OneToMany(mappedBy = "doiNha")
    private List<TranDau> tranDauDoiNhas = new ArrayList<>();

    @OneToMany(mappedBy = "doiKhach")
    private List<TranDau> tranDauDoiKhachs = new ArrayList<>();

    @OneToMany(mappedBy = "doi")
    private List<SuKienTran> suKiens = new ArrayList<>();

    public DoiBong() {}

    public Integer getId() { return id; }
    public void setId(Integer id) { this.id = id; }

    public Integer getGiaiDauId() { return giaiDauId; }
    public void setGiaiDauId(Integer giaiDauId) { this.giaiDauId = giaiDauId; }

    public Integer getBangId() { return bangId; }
    public void setBangId(Integer bangId) { this.bangId = bangId; }

    public Integer getDoiTruongId() { return doiTruongId; }
    public void setDoiTruongId(Integer doiTruongId) { this.doiTruongId = doiTruongId; }

    public String getTenDoi() { return tenDoi; }
    public void setTenDoi(String tenDoi) { this.tenDoi = tenDoi; }

    public String getLogoUrl() { return logoUrl; }
    public void setLogoUrl(String logoUrl) { this.logoUrl = logoUrl; }

    public BigDecimal getTienKyQuyConLai() { return tienKyQuyConLai; }
    public void setTienKyQuyConLai(BigDecimal tienKyQuyConLai) { this.tienKyQuyConLai = tienKyQuyConLai; }

    public Boolean getDaThanhToan() { return daThanhToan; }
    public void setDaThanhToan(Boolean daThanhToan) { this.daThanhToan = daThanhToan; }

    public LocalDateTime getThoiGianThanhToan() { return thoiGianThanhToan; }
    public void setThoiGianThanhToan(LocalDateTime thoiGianThanhToan) { this.thoiGianThanhToan = thoiGianThanhToan; }

    public String getTrangThai() { return trangThai; }
    public void setTrangThai(String trangThai) { this.trangThai = trangThai; }

    public LocalDateTime getThoiGianTao() { return thoiGianTao; }
    public void setThoiGianTao(LocalDateTime thoiGianTao) { this.thoiGianTao = thoiGianTao; }

    public GiaiDau getGiaiDau() { return giaiDau; }
    public void setGiaiDau(GiaiDau giaiDau) { this.giaiDau = giaiDau; }

    public BangDau getBang() { return bang; }
    public void setBang(BangDau bang) { this.bang = bang; }

    public Set<ThanhVienDoi> getThanhViens() { return thanhViens; }
    public void setThanhViens(Set<ThanhVienDoi> thanhViens) { this.thanhViens = thanhViens; }

    public List<TranDau> getTranDauDoiNhas() { return tranDauDoiNhas; }
    public void setTranDauDoiNhas(List<TranDau> tranDauDoiNhas) { this.tranDauDoiNhas = tranDauDoiNhas; }

    public List<TranDau> getTranDauDoiKhachs() { return tranDauDoiKhachs; }
    public void setTranDauDoiKhachs(List<TranDau> tranDauDoiKhachs) { this.tranDauDoiKhachs = tranDauDoiKhachs; }

    public List<SuKienTran> getSuKiens() { return suKiens; }
    public void setSuKiens(List<SuKienTran> suKiens) { this.suKiens = suKiens; }
}
