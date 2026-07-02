package com.pitchhub.recommendation.entity;

import jakarta.persistence.*;
import java.math.BigDecimal;
import java.time.LocalDateTime;

@Entity
@Table(name = "DatSans", schema = "dbo")
public class DatSan {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Integer id;

    @Column(name = "KhungGioId")
    private Integer khungGioId;

    @Column(name = "NgayThiDau")
    private LocalDateTime ngayThiDau;

    @Column(name = "TongTien")
    private BigDecimal tongTien;

    @Column(name = "TrangThai")
    private String trangThai;

    // Getter và Setter (bắt buộc phải có)
    public Integer getId() { return id; }
    public void setId(Integer id) { this.id = id; }
    public Integer getKhungGioId() { return khungGioId; }
    public void setKhungGioId(Integer khungGioId) { this.khungGioId = khungGioId; }
    public LocalDateTime getNgayThiDau() { return ngayThiDau; }
    public void setNgayThiDau(LocalDateTime ngayThiDau) { this.ngayThiDau = ngayThiDau; }
    public BigDecimal getTongTien() { return tongTien; }
    public void setTongTien(BigDecimal tongTien) { this.tongTien = tongTien; }
    public String getTrangThai() { return trangThai; }
    public void setTrangThai(String trangThai) { this.trangThai = trangThai; }
}