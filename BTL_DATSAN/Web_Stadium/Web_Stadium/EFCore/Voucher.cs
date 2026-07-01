using System;
using System.Collections.Generic;

namespace Web_Stadium.EFCore;

public partial class Voucher
{
    public int Id { get; set; }

    public string MaVoucher { get; set; } = null!;

    public string TenVoucher { get; set; } = null!;

    public string? MoTa { get; set; }

    public string LoaiGiam { get; set; } = null!;

    public decimal GiaTriGiam { get; set; }

    public decimal? GiamToiDa { get; set; }

    public int DiemCanDoi { get; set; }

    public int SoNgayHieuLuc { get; set; }

    public bool IsActive { get; set; }

    public DateTime NgayTao { get; set; }

    // "HeThong" = Admin tạo, "Owner" = Owner tạo
    public string LoaiVoucher { get; set; } = "HeThong";

    // null = voucher hệ thống; có giá trị = voucher của sân cụ thể
    public int? SanBongId { get; set; }

    public int? OwnerId { get; set; }

    // 0 = không giới hạn
    public int SoLuong { get; set; } = 0;

    public int DaDung { get; set; } = 0;

    public DateTime NgayBatDau { get; set; }

    public DateTime NgayHetHan { get; set; }

    // Đơn tối thiểu mới được áp dụng voucher
    public decimal DieuKienToiThieu { get; set; } = 0;

    public virtual SanBong? SanBong { get; set; }

    public virtual User? Owner { get; set; }

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();

    public virtual ICollection<DatSan> DatSanAsSan { get; set; } = new List<DatSan>();

    public virtual ICollection<DatSan> DatSanAsHeThong { get; set; } = new List<DatSan>();
}
