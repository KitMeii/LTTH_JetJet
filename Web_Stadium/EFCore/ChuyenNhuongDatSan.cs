using System;

namespace Web_Stadium.EFCore;

public partial class ChuyenNhuongDatSan
{
    public int Id { get; set; }

    public int DatSanId { get; set; }

    public int NguoiChuyenId { get; set; }

    public string? EmailNguoiNhan { get; set; }

    public string? SdtNguoiNhan { get; set; }

    public int? NguoiNhanId { get; set; }

    public string LyDo { get; set; } = null!;

    public string TrangThai { get; set; } = "ChoPheDuyet";

    public DateTime NgayTao { get; set; }

    public DateTime? NgayXuLy { get; set; }

    public int? NguoiXuLyOwnerId { get; set; }

    public string? GhiChuXuLy { get; set; }

    public virtual DatSan DatSan { get; set; } = null!;

    public virtual User NguoiChuyen { get; set; } = null!;

    public virtual User? NguoiNhan { get; set; }

    public virtual User? NguoiXuLyOwner { get; set; }
}
