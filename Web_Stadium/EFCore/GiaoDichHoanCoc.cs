using System;

namespace Web_Stadium.EFCore;

public partial class GiaoDichHoanCoc
{
    public int Id { get; set; }

    public int DatSanId { get; set; }

    public DateTime ThoiGianGiaoDich { get; set; }

    public decimal SoTien { get; set; }

    public string VaiTroNguoiKhoiTao { get; set; } = null!;

    public int? NguoiKhoiTaoId { get; set; }

    public string? TrangThaiHoan { get; set; }

    public string? GhiChu { get; set; }

    public virtual DatSan DatSan { get; set; } = null!;

    public virtual User? NguoiKhoiTao { get; set; }
}
