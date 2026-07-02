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

    public string MaXacNhan { get; set; } = null!;

    public string TrangThai { get; set; } = null!;

    public int? StaffCheckInId { get; set; }

    public int? StaffCheckOutId { get; set; }

    public string? LoaiSuCo { get; set; }

    public string? GhiChuSuCo { get; set; }

    public string? GhiChuStaff { get; set; }

    public DateTime ThoiGianTao { get; set; }

    public string? NguonHuy { get; set; }

    public string? LoaiHoanCoc { get; set; }

    public decimal? PhanTramHoan { get; set; }

    public decimal? SoTienDaHoan { get; set; }

    // Dummy Booking cho giải đấu (giai đoạn 1 blueprint)
    public int? GiaiDauId { get; set; }
    public bool LaDummyBooking { get; set; }
    public virtual GiaiDau? GiaiDau { get; set; }

    public virtual ICollection<GiaoDichHoanCoc> GiaoDichHoanCocs { get; set; } = new List<GiaoDichHoanCoc>();

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
}
