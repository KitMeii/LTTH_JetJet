namespace Web_Stadium.Services.Dto.Recommendation
{
    /// <summary>Khop dung field cua com.pitchhub.checkin.dto.BookingInfo (java-recommendation).</summary>
    public class BookingInfoDto
    {
        public int DatSanId { get; set; }
        public string? MaXacNhan { get; set; }
        public string? TrangThai { get; set; }
        public DateTime NgayThiDau { get; set; }
        public decimal TienCoc { get; set; }
        public decimal TongTien { get; set; }
        public string? HoTen { get; set; }
        public string? SoDienThoai { get; set; }
        public string? Email { get; set; }
        public string? TenSan { get; set; }
        public int SanBongId { get; set; }
        public int OwnerId { get; set; }
        public TimeOnly GioBatDau { get; set; }
        public TimeOnly GioKetThuc { get; set; }
        public decimal GiaKhungGio { get; set; }
    }
}
