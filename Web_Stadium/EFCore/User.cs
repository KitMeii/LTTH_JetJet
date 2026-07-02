using System;
using System.Collections.Generic;

namespace Web_Stadium.EFCore;

public partial class User
{
    public int Id { get; set; }

    public string HoTen { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string MatKhau { get; set; } = null!;

    public string? SoDienThoai { get; set; }

    public string VaiTro { get; set; } = null!;

    public bool IsActive { get; set; }

    public int? OwnerIdCuaStaff { get; set; }

    public DateTime NgayTao { get; set; }

    public bool DaXacThucSdt { get; set; }

    public int DiemHienTai { get; set; }

    public string? NganHang { get; set; }
    public string? SoTaiKhoan { get; set; }
    public string? TenTaiKhoan { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<DanhGia> DanhGia { get; set; } = new List<DanhGia>();

    public virtual ICollection<DatSan> DatSanStaffCheckIns { get; set; } = new List<DatSan>();

    public virtual ICollection<DatSan> DatSanStaffCheckOuts { get; set; } = new List<DatSan>();

    public virtual ICollection<DatSan> DatSanUsers { get; set; } = new List<DatSan>();

    public virtual ICollection<DiemThuongLog> DiemThuongLogs { get; set; } = new List<DiemThuongLog>();

    public virtual ICollection<User> InverseOwnerIdCuaStaffNavigation { get; set; } = new List<User>();

    public virtual ICollection<KhieuNai> KhieuNaiAdminXuLies { get; set; } = new List<KhieuNai>();

    public virtual ICollection<KhieuNai> KhieuNaiUsers { get; set; } = new List<KhieuNai>();

    public virtual ICollection<Matchmaking> Matchmakings { get; set; } = new List<Matchmaking>();

    public virtual ICollection<OtpCode> OtpCodes { get; set; } = new List<OtpCode>();

    public virtual User? OwnerIdCuaStaffNavigation { get; set; }

    public virtual ICollection<SanBong> SanBongs { get; set; } = new List<SanBong>();

    public virtual ICollection<SanYeuThich> SanYeuThiches { get; set; } = new List<SanYeuThich>();

    public virtual ICollection<StaffSanPhanCong> StaffSanPhanCongs { get; set; } = new List<StaffSanPhanCong>();

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();
}
