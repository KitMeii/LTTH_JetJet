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

    public int? OwnerId { get; set; }

    public int? SanBongId { get; set; }

    public string LoaiPhatHanh { get; set; } = "HeThong";

    public int? SoLuotConLai { get; set; }

    // ── Legacy fields (giữ để tương thích view Admin/Owner Voucher.cshtml và DuyetDon.cshtml) ──
    // "HeThong" = Admin tạo, "Owner" = Owner tạo (bản cũ, song song với LoaiPhatHanh)
    public string LoaiVoucher { get; set; } = "HeThong";

    // 0 = không giới hạn
    public int SoLuong { get; set; } = 0;

    public int DaDung { get; set; } = 0;

    public DateTime NgayBatDau { get; set; }

    public DateTime NgayHetHan { get; set; }

    // Đơn tối thiểu mới được áp dụng voucher
    public decimal DieuKienToiThieu { get; set; } = 0;

    public virtual User? Owner { get; set; }

    public virtual SanBong? SanBong { get; set; }

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();

    public virtual ICollection<DatSan> DatSanAsSan { get; set; } = new List<DatSan>();

    public virtual ICollection<DatSan> DatSanAsHeThong { get; set; } = new List<DatSan>();
}
