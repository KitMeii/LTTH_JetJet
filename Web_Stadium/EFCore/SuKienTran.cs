using System;

namespace Web_Stadium.EFCore
{
    public partial class SuKienTran
    {
        public int Id { get; set; }
        public int TranDauId { get; set; }
        public int? ThanhVienId { get; set; }
        public int? DoiId { get; set; }
        public string LoaiSuKien { get; set; } = null!;
        public int? Phut { get; set; }
        public string? GhiChu { get; set; }
        public DateTime ThoiGianGhi { get; set; }

        // Navigation
        public virtual TranDau TranDau { get; set; } = null!;
        public virtual ThanhVienDoi? ThanhVien { get; set; }
        public virtual DoiBong? Doi { get; set; }
    }
}