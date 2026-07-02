using System.Collections.Generic;

namespace Web_Stadium.EFCore
{
    public partial class ThanhVienDoi
    {
        public int Id { get; set; }
        public int DoiId { get; set; }
        public string HoTen { get; set; } = null!;
        public int SoAo { get; set; }
        public string? AnhDaiDien { get; set; }
        public int SoTranTreoGio { get; set; }
        public int TongBanThang { get; set; }
        public int TongTheVang { get; set; }
        public int TongTheDo { get; set; }

        // Navigation
        public virtual DoiBong Doi { get; set; } = null!;
        public virtual ICollection<SuKienTran> SuKiens { get; set; } = new List<SuKienTran>();
    }
}