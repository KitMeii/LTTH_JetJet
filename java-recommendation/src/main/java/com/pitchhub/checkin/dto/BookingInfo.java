package com.pitchhub.checkin.dto;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.time.LocalTime;

public class BookingInfo {
    private Integer datSanId;
    private String maXacNhan;
    private String trangThai;
    private LocalDateTime ngayThiDau;
    private BigDecimal tienCoc;
    private BigDecimal tongTien;

    private String hoTen;
    private String soDienThoai;
    private String email;

    private String tenSan;
    private Integer sanBongId;
    private Integer ownerId;

    private LocalTime gioBatDau;
    private LocalTime gioKetThuc;
    private BigDecimal giaKhungGio;

    public Integer getDatSanId() { return datSanId; }
    public void setDatSanId(Integer datSanId) { this.datSanId = datSanId; }
    public String getMaXacNhan() { return maXacNhan; }
    public void setMaXacNhan(String maXacNhan) { this.maXacNhan = maXacNhan; }
    public String getTrangThai() { return trangThai; }
    public void setTrangThai(String trangThai) { this.trangThai = trangThai; }
    public LocalDateTime getNgayThiDau() { return ngayThiDau; }
    public void setNgayThiDau(LocalDateTime ngayThiDau) { this.ngayThiDau = ngayThiDau; }
    public BigDecimal getTienCoc() { return tienCoc; }
    public void setTienCoc(BigDecimal tienCoc) { this.tienCoc = tienCoc; }
    public BigDecimal getTongTien() { return tongTien; }
    public void setTongTien(BigDecimal tongTien) { this.tongTien = tongTien; }
    public String getHoTen() { return hoTen; }
    public void setHoTen(String hoTen) { this.hoTen = hoTen; }
    public String getSoDienThoai() { return soDienThoai; }
    public void setSoDienThoai(String soDienThoai) { this.soDienThoai = soDienThoai; }
    public String getEmail() { return email; }
    public void setEmail(String email) { this.email = email; }
    public String getTenSan() { return tenSan; }
    public void setTenSan(String tenSan) { this.tenSan = tenSan; }
    public Integer getSanBongId() { return sanBongId; }
    public void setSanBongId(Integer sanBongId) { this.sanBongId = sanBongId; }
    public Integer getOwnerId() { return ownerId; }
    public void setOwnerId(Integer ownerId) { this.ownerId = ownerId; }
    public LocalTime getGioBatDau() { return gioBatDau; }
    public void setGioBatDau(LocalTime gioBatDau) { this.gioBatDau = gioBatDau; }
    public LocalTime getGioKetThuc() { return gioKetThuc; }
    public void setGioKetThuc(LocalTime gioKetThuc) { this.gioKetThuc = gioKetThuc; }
    public BigDecimal getGiaKhungGio() { return giaKhungGio; }
    public void setGiaKhungGio(BigDecimal giaKhungGio) { this.giaKhungGio = giaKhungGio; }
}
