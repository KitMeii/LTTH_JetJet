using System;
using System.Collections.Generic;

namespace Web_Stadium.EFCore
{
    public partial class DoiBong
    {
        public int Id { get; set; }
        public int GiaiDauId { get; set; }
        public int? BangId { get; set; }
        public int DoiTruongId { get; set; }
        public string TenDoi { get; set; } = null!;
        public string? LogoUrl { get; set; }
        public decimal TienKyQuyConLai { get; set; }
        public bool DaThanhToan { get; set; }
        public DateTime? ThoiGianThanhToan { get; set; }
        public string TrangThai { get; set; } = "Active";
        public DateTime ThoiGianTao { get; set; }

        // Navigation
        public virtual GiaiDau GiaiDau { get; set; } = null!;
        public virtual BangDau? Bang { get; set; }
        public virtual User? DoiTruong { get; set; }
        public virtual ICollection<ThanhVienDoi> ThanhViens { get; set; } = new List<ThanhVienDoi>();
        public virtual ICollection<TranDau> TranDauDoiNhas { get; set; } = new List<TranDau>();
        public virtual ICollection<TranDau> TranDauDoiKhachs { get; set; } = new List<TranDau>();
        public virtual ICollection<SuKienTran> SuKiens { get; set; } = new List<SuKienTran>();
    }
}