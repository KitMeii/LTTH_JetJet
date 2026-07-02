using System;
using System.Collections.Generic;

namespace Web_Stadium.EFCore
{
    public partial class TranDau
    {
        public int Id { get; set; }
        public int GiaiDauId { get; set; }
        public int? BangId { get; set; }
        public int? KhungGioId { get; set; }
        public int DoiNhaId { get; set; }
        public int DoiKhachId { get; set; }
        public int? BanThangNha { get; set; }
        public int? BanThangKhach { get; set; }
        public int VongDau { get; set; }
        public string LoaiVong { get; set; } = "VongBang";
        public DateTime NgayThiDau { get; set; }
        public string TrangThai { get; set; } = "Scheduled";
        public int? StaffPhuTrachId { get; set; }

        // Navigation
        public virtual GiaiDau GiaiDau { get; set; } = null!;
        public virtual BangDau? BangDau { get; set; }
        public virtual KhungGio? KhungGio { get; set; }
        public virtual DoiBong? DoiNha { get; set; }
        public virtual DoiBong? DoiKhach { get; set; }
        public virtual User? StaffPhuTrach { get; set; }
        public virtual ICollection<SuKienTran> SuKiens { get; set; } = new List<SuKienTran>();
    }
}