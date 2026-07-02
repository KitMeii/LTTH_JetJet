using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Stadium.EFCore
{
    [Table("AnhSanBongs")]
    public class AnhSanBong
    {
        [Key]
        public int Id { get; set; }
        public int SanBongId { get; set; }
        public string DuongDan { get; set; } = "";
        public string LoaiAnh { get; set; } = "Upload"; // 'Upload' hoặc 'URL'
        public int ThuTu { get; set; } = 0;
        public string? MoTa { get; set; }
        public DateTime NgayThem { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(SanBongId))]
        public virtual SanBong SanBong { get; set; }
    }
}
