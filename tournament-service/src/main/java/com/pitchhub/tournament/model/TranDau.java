package com.pitchhub.tournament.model;

import jakarta.persistence.*;
import org.hibernate.annotations.DynamicInsert;

import java.time.LocalDateTime;
import java.util.LinkedHashSet;
import java.util.Set;

/**
 * Map bang TranDaus (tran dau trong giai — vong bang + knock-out).
 * @DynamicInsert: DoiNhaId/DoiKhachId la NOT NULL DEFAULT 0 trong schema
 * (0 = TBD, xem SanBongBTL.sql) — tran placeholder knock-out (Ban ket/Chung
 * ket cho ket qua) duoc tao voi doiNha/doiKhach = null; neu INSERT gui NULL
 * tuong minh se vi pham NOT NULL. DynamicInsert bo qua cot co gia tri null,
 * de DB tu ap DEFAULT 0.
 */
@Entity
@Table(name = "TranDaus")
@DynamicInsert
public class TranDau {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Integer id;

    @Column(name = "GiaiDauId", nullable = false, insertable = false, updatable = false)
    private Integer giaiDauId;

    @Column(name = "BangId", insertable = false, updatable = false)
    private Integer bangId;

    @Column(name = "KhungGioId")
    private Integer khungGioId;

    @Column(name = "DoiNhaId", insertable = false, updatable = false)
    private Integer doiNhaId;

    @Column(name = "DoiKhachId", insertable = false, updatable = false)
    private Integer doiKhachId;

    @Column(name = "BanThangNha")
    private Integer banThangNha;

    @Column(name = "BanThangKhach")
    private Integer banThangKhach;

    @Column(name = "VongDau", nullable = false)
    private Integer vongDau;

    /** VongBang | TuKet | BanKet | ChungKet */
    @Column(name = "LoaiVong", nullable = false)
    private String loaiVong = "VongBang";

    @Column(name = "NgayThiDau", nullable = false)
    private LocalDateTime ngayThiDau;

    /** Scheduled | InProgress | Closed | Pending (cho doi tu vong truoc knock-out) */
    @Column(name = "TrangThai", nullable = false)
    private String trangThai = "Scheduled";

    @Column(name = "StaffPhuTrachId")
    private Integer staffPhuTrachId;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "GiaiDauId", nullable = false)
    private GiaiDau giaiDau;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "BangId")
    private BangDau bangDau;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "DoiNhaId")
    private DoiBong doiNha;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "DoiKhachId")
    private DoiBong doiKhach;

    // Set — cung ly do voi GiaiDau.doiBongs (tranh MultipleBagFetchException khi
    // @EntityGraph fetch dong thoi "tranDaus" + "tranDaus.suKiens").
    @OneToMany(mappedBy = "tranDau", cascade = CascadeType.ALL, orphanRemoval = true)
    private Set<SuKienTran> suKiens = new LinkedHashSet<>();

    public TranDau() {}

    public Integer getId() { return id; }
    public void setId(Integer id) { this.id = id; }

    public Integer getGiaiDauId() { return giaiDauId; }
    public void setGiaiDauId(Integer giaiDauId) { this.giaiDauId = giaiDauId; }

    public Integer getBangId() { return bangId; }
    public void setBangId(Integer bangId) { this.bangId = bangId; }

    public Integer getKhungGioId() { return khungGioId; }
    public void setKhungGioId(Integer khungGioId) { this.khungGioId = khungGioId; }

    public Integer getDoiNhaId() { return doiNhaId; }
    public void setDoiNhaId(Integer doiNhaId) { this.doiNhaId = doiNhaId; }

    public Integer getDoiKhachId() { return doiKhachId; }
    public void setDoiKhachId(Integer doiKhachId) { this.doiKhachId = doiKhachId; }

    public Integer getBanThangNha() { return banThangNha; }
    public void setBanThangNha(Integer banThangNha) { this.banThangNha = banThangNha; }

    public Integer getBanThangKhach() { return banThangKhach; }
    public void setBanThangKhach(Integer banThangKhach) { this.banThangKhach = banThangKhach; }

    public Integer getVongDau() { return vongDau; }
    public void setVongDau(Integer vongDau) { this.vongDau = vongDau; }

    public String getLoaiVong() { return loaiVong; }
    public void setLoaiVong(String loaiVong) { this.loaiVong = loaiVong; }

    public LocalDateTime getNgayThiDau() { return ngayThiDau; }
    public void setNgayThiDau(LocalDateTime ngayThiDau) { this.ngayThiDau = ngayThiDau; }

    public String getTrangThai() { return trangThai; }
    public void setTrangThai(String trangThai) { this.trangThai = trangThai; }

    public Integer getStaffPhuTrachId() { return staffPhuTrachId; }
    public void setStaffPhuTrachId(Integer staffPhuTrachId) { this.staffPhuTrachId = staffPhuTrachId; }

    public GiaiDau getGiaiDau() { return giaiDau; }
    public void setGiaiDau(GiaiDau giaiDau) { this.giaiDau = giaiDau; }

    public BangDau getBangDau() { return bangDau; }
    public void setBangDau(BangDau bangDau) { this.bangDau = bangDau; }

    public DoiBong getDoiNha() { return doiNha; }
    public void setDoiNha(DoiBong doiNha) { this.doiNha = doiNha; }

    public DoiBong getDoiKhach() { return doiKhach; }
    public void setDoiKhach(DoiBong doiKhach) { this.doiKhach = doiKhach; }

    public Set<SuKienTran> getSuKiens() { return suKiens; }
    public void setSuKiens(Set<SuKienTran> suKiens) { this.suKiens = suKiens; }
}
