using System.Collections.Generic;

namespace Web_Stadium.EFCore
{
    public partial class BangDau
    {
        public int Id { get; set; }
        public int GiaiDauId { get; set; }
        public string TenBang { get; set; } = null!;

        // Navigation
        public virtual GiaiDau GiaiDau { get; set; } = null!;
        public virtual ICollection<DoiBong> DoiBongs { get; set; } = new List<DoiBong>();
        public virtual ICollection<TranDau> TranDaus { get; set; } = new List<TranDau>();
    }
}