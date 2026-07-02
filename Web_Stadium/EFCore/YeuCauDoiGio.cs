using System;

namespace Web_Stadium.EFCore;

public partial class YeuCauDoiGio
{
    public int Id { get; set; }

    public int DatSanId { get; set; }

    public int KhungGioMoiId { get; set; }

    public DateTime NgayThiDauMoi { get; set; }

    public string LyDo { get; set; } = null!;

    public string TrangThai { get; set; } = "ChoPheDuyet";

    public DateTime NgayTao { get; set; }

    public DateTime? NgayXuLy { get; set; }

    public int? NguoiXuLyId { get; set; }

    public string? GhiChuXuLy { get; set; }

    public virtual DatSan DatSan { get; set; } = null!;

    public virtual KhungGio KhungGioMoi { get; set; } = null!;

    public virtual User? NguoiXuLy { get; set; }
}
