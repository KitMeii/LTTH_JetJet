using System;
using System.Collections.Generic;

namespace Web_Stadium.EFCore
{
    public partial class GiaiDau
    {
        public int Id { get; set; }
        public string TenGiai { get; set; } = null!;
        public string? MoTa { get; set; }
        public int SanBongId { get; set; }
        public int OwnerId { get; set; }
        public int SoDoiToiDa { get; set; }
        public int SoBang { get; set; }
        public decimal LePhiGiai { get; set; }
        public decimal TienKyQuy { get; set; }
        public decimal TienPhatTheVang { get; set; }
        public decimal TienPhatTheDo { get; set; }
        public int SoTranTreoGioTheDo { get; set; }
        public int SoTheVangTichLuy { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public DateTime ThoiGianTao { get; set; }
        public DateTime? ThoiGianDongDanhSach { get; set; }
        public string TrangThai { get; set; } = "Draft";

        // Staff phụ trách toàn giải (Owner gán ở giai đoạn 3)
        public int? StaffPhuTrachId { get; set; }

        // Lịch slot Owner đã block lúc tạo giải (JSON: [{khungGioId, ngay}])
        public string? LichBlockJson { get; set; }

        // Navigation
        public virtual SanBong? SanBong { get; set; }
        public virtual User? Owner { get; set; }
        public virtual User? StaffPhuTrach { get; set; }
        public virtual ICollection<BangDau> BangDaus { get; set; } = new List<BangDau>();
        public virtual ICollection<DoiBong> DoiBongs { get; set; } = new List<DoiBong>();
        public virtual ICollection<TranDau> TranDaus { get; set; } = new List<TranDau>();
        public virtual ICollection<DatSan> DatSans { get; set; } = new List<DatSan>();
    }
}