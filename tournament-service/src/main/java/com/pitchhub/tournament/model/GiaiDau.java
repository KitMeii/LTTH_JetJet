package com.pitchhub.tournament.model;

import jakarta.persistence.*;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.util.LinkedHashSet;
import java.util.Set;

/** Map bang GiaiDaus — giu nguyen ten cot tieng Viet nhu C# EFCore/GiaiDau.cs. */
@Entity
@Table(name = "GiaiDaus")
public class GiaiDau {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Integer id;

    @Column(name = "TenGiai", nullable = false)
    private String tenGiai;

    @Column(name = "MoTa")
    private String moTa;

    @Column(name = "SanBongId", nullable = false)
    private Integer sanBongId;

    @Column(name = "OwnerId", nullable = false)
    private Integer ownerId;

    @Column(name = "SoDoiToiDa", nullable = false)
    private Integer soDoiToiDa;

    @Column(name = "SoBang", nullable = false)
    private Integer soBang;

    @Column(name = "LePhiGiai", nullable = false, precision = 18, scale = 2)
    private BigDecimal lePhiGiai;

    @Column(name = "TienKyQuy", nullable = false, precision = 18, scale = 2)
    private BigDecimal tienKyQuy;

    @Column(name = "TienPhatTheVang", nullable = false, precision = 18, scale = 2)
    private BigDecimal tienPhatTheVang;

    @Column(name = "TienPhatTheDo", nullable = false, precision = 18, scale = 2)
    private BigDecimal tienPhatTheDo;

    @Column(name = "SoTranTreoGioTheDo", nullable = false)
    private Integer soTranTreoGioTheDo;

    @Column(name = "SoTheVangTichLuy", nullable = false)
    private Integer soTheVangTichLuy;

    @Column(name = "NgayBatDau", nullable = false)
    private LocalDateTime ngayBatDau;

    @Column(name = "NgayKetThuc", nullable = false)
    private LocalDateTime ngayKetThuc;

    @Column(name = "ThoiGianTao", nullable = false)
    private LocalDateTime thoiGianTao;

    @Column(name = "ThoiGianDongDanhSach")
    private LocalDateTime thoiGianDongDanhSach;

    /** Draft -> Approved -> RegistrationOpen -> RegistrationClosed -> Active -> Finished */
    @Column(name = "TrangThai", nullable = false)
    private String trangThai = "Draft";

    // Set (khong phai List) — Hibernate khong the JOIN FETCH nhieu "bag" (List)
    // cung luc trong 1 query (@EntityGraph goi dong thoi bangDaus/doiBongs/tranDaus).
    @OneToMany(mappedBy = "giaiDau", cascade = CascadeType.ALL, orphanRemoval = true)
    private Set<BangDau> bangDaus = new LinkedHashSet<>();

    @OneToMany(mappedBy = "giaiDau", cascade = CascadeType.ALL, orphanRemoval = true)
    private Set<DoiBong> doiBongs = new LinkedHashSet<>();

    @OneToMany(mappedBy = "giaiDau", cascade = CascadeType.ALL, orphanRemoval = true)
    private Set<TranDau> tranDaus = new LinkedHashSet<>();

    public GiaiDau() {}

    public Integer getId() { return id; }
    public void setId(Integer id) { this.id = id; }

    public String getTenGiai() { return tenGiai; }
    public void setTenGiai(String tenGiai) { this.tenGiai = tenGiai; }

    public String getMoTa() { return moTa; }
    public void setMoTa(String moTa) { this.moTa = moTa; }

    public Integer getSanBongId() { return sanBongId; }
    public void setSanBongId(Integer sanBongId) { this.sanBongId = sanBongId; }

    public Integer getOwnerId() { return ownerId; }
    public void setOwnerId(Integer ownerId) { this.ownerId = ownerId; }

    public Integer getSoDoiToiDa() { return soDoiToiDa; }
    public void setSoDoiToiDa(Integer soDoiToiDa) { this.soDoiToiDa = soDoiToiDa; }

    public Integer getSoBang() { return soBang; }
    public void setSoBang(Integer soBang) { this.soBang = soBang; }

    public BigDecimal getLePhiGiai() { return lePhiGiai; }
    public void setLePhiGiai(BigDecimal lePhiGiai) { this.lePhiGiai = lePhiGiai; }

    public BigDecimal getTienKyQuy() { return tienKyQuy; }
    public void setTienKyQuy(BigDecimal tienKyQuy) { this.tienKyQuy = tienKyQuy; }

    public BigDecimal getTienPhatTheVang() { return tienPhatTheVang; }
    public void setTienPhatTheVang(BigDecimal tienPhatTheVang) { this.tienPhatTheVang = tienPhatTheVang; }

    public BigDecimal getTienPhatTheDo() { return tienPhatTheDo; }
    public void setTienPhatTheDo(BigDecimal tienPhatTheDo) { this.tienPhatTheDo = tienPhatTheDo; }

    public Integer getSoTranTreoGioTheDo() { return soTranTreoGioTheDo; }
    public void setSoTranTreoGioTheDo(Integer soTranTreoGioTheDo) { this.soTranTreoGioTheDo = soTranTreoGioTheDo; }

    public Integer getSoTheVangTichLuy() { return soTheVangTichLuy; }
    public void setSoTheVangTichLuy(Integer soTheVangTichLuy) { this.soTheVangTichLuy = soTheVangTichLuy; }

    public LocalDateTime getNgayBatDau() { return ngayBatDau; }
    public void setNgayBatDau(LocalDateTime ngayBatDau) { this.ngayBatDau = ngayBatDau; }

    public LocalDateTime getNgayKetThuc() { return ngayKetThuc; }
    public void setNgayKetThuc(LocalDateTime ngayKetThuc) { this.ngayKetThuc = ngayKetThuc; }

    public LocalDateTime getThoiGianTao() { return thoiGianTao; }
    public void setThoiGianTao(LocalDateTime thoiGianTao) { this.thoiGianTao = thoiGianTao; }

    public LocalDateTime getThoiGianDongDanhSach() { return thoiGianDongDanhSach; }
    public void setThoiGianDongDanhSach(LocalDateTime thoiGianDongDanhSach) { this.thoiGianDongDanhSach = thoiGianDongDanhSach; }

    public String getTrangThai() { return trangThai; }
    public void setTrangThai(String trangThai) { this.trangThai = trangThai; }

    public Set<BangDau> getBangDaus() { return bangDaus; }
    public void setBangDaus(Set<BangDau> bangDaus) { this.bangDaus = bangDaus; }

    public Set<DoiBong> getDoiBongs() { return doiBongs; }
    public void setDoiBongs(Set<DoiBong> doiBongs) { this.doiBongs = doiBongs; }

    public Set<TranDau> getTranDaus() { return tranDaus; }
    public void setTranDaus(Set<TranDau> tranDaus) { this.tranDaus = tranDaus; }
}
