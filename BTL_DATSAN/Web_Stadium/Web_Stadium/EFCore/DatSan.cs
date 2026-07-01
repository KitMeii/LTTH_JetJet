using System;
using System.Collections.Generic;

namespace Web_Stadium.EFCore;

public partial class DatSan
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int KhungGioId { get; set; }

    public DateTime NgayThiDau { get; set; }

    public decimal TienCoc { get; set; }

    public decimal TongTien { get; set; }

    // Giá gốc trước khi áp voucher
    public decimal TienGoc { get; set; }

    // Tiền giảm từ voucher Owner (sân)
    public decimal TienGiamSan { get; set; }

    // Tiền giảm từ voucher Hệ thống (Admin)
    public decimal TienGiamHeThong { get; set; }

    // FK → Voucher Owner
    public int? VoucherSanId { get; set; }

    // FK → Voucher Hệ thống
    public int? VoucherHeThongId { get; set; }

    public string MaXacNhan { get; set; } = null!;

    public string TrangThai { get; set; } = null!;

    public int? StaffCheckInId { get; set; }

    public int? StaffCheckOutId { get; set; }

    public string? LoaiSuCo { get; set; }

    public string? GhiChuSuCo { get; set; }

    public DateTime ThoiGianTao { get; set; }

    public virtual ICollection<DanhGia> DanhGia { get; set; } = new List<DanhGia>();

    public virtual ICollection<DatSanDichVu> DatSanDichVus { get; set; } = new List<DatSanDichVu>();

    public virtual ICollection<DiemThuongLog> DiemThuongLogs { get; set; } = new List<DiemThuongLog>();

    public virtual ICollection<KhieuNai> KhieuNais { get; set; } = new List<KhieuNai>();

    public virtual KhungGio KhungGio { get; set; } = null!;

    public virtual Matchmaking? Matchmaking { get; set; }

    public virtual User? StaffCheckIn { get; set; }

    public virtual User? StaffCheckOut { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();

    public virtual Voucher? VoucherSan { get; set; }

    public virtual Voucher? VoucherHeThong { get; set; }
}
