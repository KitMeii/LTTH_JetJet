using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Hubs;

namespace Web_Stadium.Services
{
    public class TournamentService
    {
        // Dùng chung cho mọi deserialize lịch block — chấp nhận cả camelCase từ JS
        private static readonly JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        private readonly SanBongContext _context;
        private readonly ScheduleService _scheduleService;
        private readonly StandingService _standingService;
        private readonly SuspensionService _suspensionService;
        private readonly TournamentNotificationService _notificationService;
        private readonly KnockOutService _knockOutService;
        private readonly IHubContext<TournamentHub> _hubContext;

        public TournamentService(
            SanBongContext context,
            ScheduleService scheduleService,
            StandingService standingService,
            SuspensionService suspensionService,
            TournamentNotificationService notificationService,
            KnockOutService knockOutService,
            IHubContext<TournamentHub> hubContext)
        {
            _context = context;
            _scheduleService = scheduleService;
            _standingService = standingService;
            _suspensionService = suspensionService;
            _notificationService = notificationService;
            _knockOutService = knockOutService;
            _hubContext = hubContext;
        }

        // ══════════════════════════════════════════════════════════
        // Tạo giải đấu + tự sinh các bảng A/B/C...
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error, GiaiDau? giai)> TaoGiaiDau(
            CreateGiaiDauDto dto, int ownerId)
        {
            var san = await _context.SanBongs.FirstOrDefaultAsync(s =>
                s.Id == dto.SanBongId &&
                s.OwnerId == ownerId &&
                s.TrangThaiDuyet == "DaDuyet" &&
                !s.IsHidden);

            if (san == null)
                return (false, "Sân không hợp lệ hoặc không thuộc quyền quản lý của bạn!", null);

            if (dto.NgayBatDau < DateTime.Today)
                return (false, "Ngày bắt đầu không được là ngày đã qua!", null);

            if (dto.NgayKetThuc <= dto.NgayBatDau)
                return (false, "Ngày kết thúc phải sau ngày bắt đầu!", null);

            var soDoiHopLe = new[] { 4, 8, 16, 32 };
            if (!soDoiHopLe.Contains(dto.SoDoiToiDa))
                return (false, "Số đội tối đa phải là 4, 8, 16 hoặc 32!", null);

            // Validate lịch block (nếu có) — toàn bộ slot phải thuộc sân & nằm trong khoảng giải
            string? lichBlockJson = null;
            if (dto.LichBlock != null && dto.LichBlock.Count > 0)
            {
                var khungGioIds = dto.LichBlock.Select(s => s.KhungGioId).Distinct().ToList();
                var soKhungHopLe = await _context.KhungGios
                    .CountAsync(k => khungGioIds.Contains(k.Id) && k.SanBongId == dto.SanBongId);
                if (soKhungHopLe != khungGioIds.Count)
                    return (false, "Có khung giờ không thuộc sân đã chọn!", null);

                if (dto.LichBlock.Any(s => s.Ngay.Date < dto.NgayBatDau.Date || s.Ngay.Date > dto.NgayKetThuc.Date))
                    return (false, "Có slot block nằm ngoài khoảng ngày giải đấu!", null);

                lichBlockJson = JsonSerializer.Serialize(dto.LichBlock);
            }

            var giai = new GiaiDau
            {
                TenGiai = dto.TenGiai.Trim(),
                MoTa = dto.MoTa?.Trim(),
                SanBongId = dto.SanBongId,
                OwnerId = ownerId,
                SoDoiToiDa = dto.SoDoiToiDa,
                SoBang = dto.SoBang,
                LePhiGiai = dto.LePhiGiai,
                TienKyQuy = dto.TienKyQuy,
                TienPhatTheVang = dto.TienPhatTheVang > 0 ? dto.TienPhatTheVang : 20000m,
                TienPhatTheDo = dto.TienPhatTheDo > 0 ? dto.TienPhatTheDo : 100000m,
                SoTranTreoGioTheDo = dto.SoTranTreoGioTheDo > 0 ? dto.SoTranTreoGioTheDo : 1,
                SoTheVangTichLuy = dto.SoTheVangTichLuy > 0 ? dto.SoTheVangTichLuy : 2,
                NgayBatDau = dto.NgayBatDau,
                NgayKetThuc = dto.NgayKetThuc,
                ThoiGianDongDanhSach = dto.ThoiGianDong ?? dto.NgayBatDau.AddDays(-1),
                LichBlockJson = lichBlockJson,
                TrangThai = "Draft",
                ThoiGianTao = DateTime.Now
            };

            _context.GiaiDaus.Add(giai);
            await _context.SaveChangesAsync();

            for (int i = 0; i < dto.SoBang; i++)
            {
                _context.BangDaus.Add(new BangDau
                {
                    GiaiDauId = giai.Id,
                    TenBang = "Bảng " + (char)('A' + i)
                });
            }
            await _context.SaveChangesAsync();

            return (true, "", giai);
        }

        // ══════════════════════════════════════════════════════════
        // Cập nhật lịch block sau khi giải đã tạo
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> CapNhatLichBlock(
            int giaiId, int ownerId, List<ScheduleService.SlotKhungGio> lichBlock)
        {
            var giai = await LayGiaiCuaOwner(giaiId, ownerId);
            if (giai == null) return (false, "Không tìm thấy giải!");
            if (giai.TrangThai is not ("Draft" or "RegistrationOpen" or "RegistrationClosed"))
                return (false, "Không thể đổi lịch block sau khi giải đã khởi tạo!");

            if (lichBlock.Any(s => s.Ngay.Date < giai.NgayBatDau.Date || s.Ngay.Date > giai.NgayKetThuc.Date))
                return (false, "Có slot nằm ngoài khoảng ngày giải!");

            var khungGioIds = lichBlock.Select(s => s.KhungGioId).Distinct().ToList();
            var hopLe = await _context.KhungGios
                .CountAsync(k => khungGioIds.Contains(k.Id) && k.SanBongId == giai.SanBongId);
            if (hopLe != khungGioIds.Count)
                return (false, "Có khung giờ không thuộc sân của giải!");

            giai.LichBlockJson = JsonSerializer.Serialize(lichBlock);
            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // Owner xác nhận đã nhận tiền của 1 đội (chuyển khoản tay)
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error, DoiBong? doi)> XacNhanThanhToanDoi(int doiId, int ownerId)
        {
            var doi = await _context.DoiBongs
                .Include(d => d.GiaiDau)
                .Include(d => d.DoiTruong)
                .FirstOrDefaultAsync(d => d.Id == doiId && d.GiaiDau.OwnerId == ownerId);

            if (doi == null) return (false, "Không tìm thấy đội!", null);
            if (doi.DaThanhToan) return (false, "Đội này đã được xác nhận rồi!", null);
            if (doi.GiaiDau.TrangThai != "RegistrationOpen")
                return (false, "Chỉ xác nhận khi giải đang mở đăng ký!", null);

            doi.DaThanhToan = true;
            doi.ThoiGianThanhToan = DateTime.Now;
            doi.TienKyQuyConLai = doi.GiaiDau.TienKyQuy;
            await _context.SaveChangesAsync();

            // Email xác nhận (fire & forget)
            _ = _notificationService.GuiEmailXacNhanDangKy(doiId);

            return (true, "", doi);
        }

        // ══════════════════════════════════════════════════════════
        // Owner hủy đăng ký 1 đội khi giải đang mở
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error, int giaiId)> HuyDangKyDoi(int doiId, string lyDo, int ownerId)
        {
            var doi = await _context.DoiBongs
                .Include(d => d.GiaiDau)
                .Include(d => d.ThanhViens)
                .FirstOrDefaultAsync(d => d.Id == doiId && d.GiaiDau.OwnerId == ownerId);

            if (doi == null) return (false, "Không tìm thấy đội!", 0);
            if (doi.GiaiDau.TrangThai != "RegistrationOpen")
                return (false, "Chỉ hủy đăng ký khi giải đang mở!", 0);
            if (string.IsNullOrWhiteSpace(lyDo))
                return (false, "Phải nhập lý do hủy!", 0);

            var giaiId = doi.GiaiDauId;
            _context.ThanhVienDois.RemoveRange(doi.ThanhViens);
            _context.DoiBongs.Remove(doi);
            await _context.SaveChangesAsync();
            return (true, "", giaiId);
        }

        // ══════════════════════════════════════════════════════════
        // Mở đăng ký: Draft / Approved → RegistrationOpen
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> MoiDangKy(int giaiId, int ownerId)
        {
            var giai = await LayGiaiCuaOwner(giaiId, ownerId);
            if (giai == null) return (false, "Không tìm thấy giải!");

            if (giai.TrangThai is not ("Draft" or "Approved"))
                return (false, "Chỉ mở đăng ký khi giải ở trạng thái Draft hoặc Approved!");

            giai.TrangThai = "RegistrationOpen";
            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // Đóng đăng ký: RegistrationOpen → RegistrationClosed
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> DongDangKy(int giaiId, int ownerId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.DoiBongs)
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);

            if (giai == null) return (false, "Không tìm thấy giải!");
            if (giai.TrangThai != "RegistrationOpen")
                return (false, "Chỉ đóng đăng ký khi giải đang mở!");

            var soDoiHopLe = giai.DoiBongs.Count(d => d.DaThanhToan);
            if (soDoiHopLe < 2)
                return (false, "Cần ít nhất 2 đội đã thanh toán để đóng đăng ký!");

            giai.TrangThai = "RegistrationClosed";
            giai.ThoiGianDongDanhSach = DateTime.Now;
            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // Gán đội vào bảng (Drag & Drop)
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> GanDoiVaoBang(
            int doiId, int? bangId, int ownerId)
        {
            var doi = await _context.DoiBongs
                .Include(d => d.GiaiDau)
                .FirstOrDefaultAsync(d => d.Id == doiId && d.GiaiDau.OwnerId == ownerId);

            if (doi == null) return (false, "Không tìm thấy đội!");
            if (doi.GiaiDau.TrangThai != "RegistrationClosed")
                return (false, "Chỉ chia bảng khi đã đóng đăng ký!");

            if (bangId.HasValue)
            {
                var bang = await _context.BangDaus
                    .FirstOrDefaultAsync(b => b.Id == bangId && b.GiaiDauId == doi.GiaiDauId);
                if (bang == null) return (false, "Bảng không hợp lệ!");
            }

            doi.BangId = bangId;
            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // Khởi tạo giải: sinh lịch + map slot + dummy booking + email
        // RegistrationClosed → Active
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> KhoiTaoGiai(int giaiId, int ownerId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs).ThenInclude(d => d.Bang)
                .Include(g => g.DoiBongs).ThenInclude(d => d.DoiTruong)
                .Include(g => g.SanBong).ThenInclude(s => s.KhungGios)
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);

            if (giai == null) return (false, "Không tìm thấy giải!");
            if (giai.TrangThai != "RegistrationClosed")
                return (false, "Chỉ khởi tạo sau khi đóng đăng ký!");

            var doiChuaBang = giai.DoiBongs.Where(d => d.BangId == null && d.DaThanhToan).ToList();
            if (doiChuaBang.Any())
                return (false, $"Còn {doiChuaBang.Count} đội chưa được xếp bảng!");

            // Đọc lịch block đã lưu (nếu có)
            List<ScheduleService.SlotKhungGio>? lichBlock = null;
            if (!string.IsNullOrEmpty(giai.LichBlockJson))
            {
                try
                {
                    lichBlock = JsonSerializer.Deserialize<List<ScheduleService.SlotKhungGio>>(giai.LichBlockJson, _jsonOpts);
                }
                catch { lichBlock = null; }
            }

            var tranDaus = _scheduleService.SinhLichVongTron(giai, lichBlock);

            if (lichBlock != null && lichBlock.Count < tranDaus.Count)
                return (false, $"Lịch block chưa đủ slot ({lichBlock.Count}/{tranDaus.Count} trận)! Vào Lịch Block để bổ sung trước khi khởi tạo.");

            _context.TranDaus.AddRange(tranDaus);
            await _context.SaveChangesAsync();

            // Tạo Dummy Booking để khóa slot khỏi khách vãng lai
            if (lichBlock != null)
            {
                foreach (var slot in lichBlock)
                {
                    _context.DatSans.Add(new DatSan
                    {
                        UserId = ownerId,
                        KhungGioId = slot.KhungGioId,
                        NgayThiDau = slot.Ngay.Date,
                        TienCoc = 0,
                        TongTien = 0,
                        MaXacNhan = $"GIAI-{giai.Id}-{Guid.NewGuid():N}".Substring(0, 16),
                        TrangThai = "DaXacNhan",
                        ThoiGianTao = DateTime.Now,
                        GiaiDauId = giai.Id,
                        LaDummyBooking = true
                    });
                }
                await _context.SaveChangesAsync();
            }

            giai.TrangThai = "Active";
            await _context.SaveChangesAsync();

            // Email lịch đấu (fire & forget)
            _ = _notificationService.GuiEmailLichDau(giaiId);

            // Realtime: thông báo giải bắt đầu
            try
            {
                var bxh = await _standingService.GetStandings(giaiId);
                await TournamentHub.BroadcastBXH(_hubContext, giaiId, bxh);
            }
            catch { }

            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // Kết thúc giải: Active → Finished, hủy DummyBooking dư
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> KetThucGiai(int giaiId, int ownerId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.TranDaus)
                .Include(g => g.DatSans)
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);

            if (giai == null) return (false, "Không tìm thấy giải!");
            if (giai.TrangThai != "Active")
                return (false, "Chỉ kết thúc khi giải đang Active!");

            var tranChuaXong = giai.TranDaus
                .Count(t => t.TrangThai is "Scheduled" or "InProgress");
            if (tranChuaXong > 0)
                return (false, $"Còn {tranChuaXong} trận chưa kết thúc!");

            giai.TrangThai = "Finished";

            // Hủy DummyBooking còn chưa dùng (slot tương lai)
            var hom = DateTime.Today;
            foreach (var ds in giai.DatSans.Where(d => d.LaDummyBooking && d.TrangThai != "DaHuy"))
            {
                if (ds.NgayThiDau.Date >= hom)
                {
                    ds.TrangThai = "DaHuy";
                    ds.NguonHuy = "GiaiKetThuc";
                }
            }

            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // Owner gán Staff phụ trách toàn giải
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> GanStaffPhuTrach(int giaiId, int staffId, int ownerId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.TranDaus)
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);
            if (giai == null) return (false, "Không tìm thấy giải!");

            // Staff phải được phân công tại sân của Owner
            var sanCuaToi = await _context.SanBongs
                .Where(s => s.OwnerId == ownerId)
                .Select(s => s.Id)
                .ToListAsync();

            var hopLe = await _context.StaffSanPhanCongs
                .AnyAsync(p => p.StaffId == staffId && sanCuaToi.Contains(p.SanBongId));
            if (!hopLe) return (false, "Staff không được phân công tại sân của bạn!");

            giai.StaffPhuTrachId = staffId;

            // Mặc định gán cho mọi trận chưa có người phụ trách
            foreach (var tran in giai.TranDaus.Where(t => t.StaffPhuTrachId == null))
                tran.StaffPhuTrachId = staffId;

            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // Owner gán khung giờ cho 1 trận cụ thể
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error, KhungGio? kg, DateTime ngay)> GanKhungGioTran(
            int tranDauId, int khungGioId, DateTime ngay, int ownerId)
        {
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau)
                .FirstOrDefaultAsync(t => t.Id == tranDauId && t.GiaiDau.OwnerId == ownerId);
            if (tran == null) return (false, "Không tìm thấy trận!", null, default);
            if (tran.TrangThai != "Scheduled" && tran.TrangThai != "Pending")
                return (false, "Trận đã bắt đầu, không thể đổi giờ!", null, default);

            var kg = await _context.KhungGios.FirstOrDefaultAsync(k =>
                k.Id == khungGioId && k.SanBongId == tran.GiaiDau.SanBongId);
            if (kg == null) return (false, "Khung giờ không thuộc sân của giải!", null, default);

            var trungLich = await _context.TranDaus.AnyAsync(t =>
                t.Id != tranDauId &&
                t.GiaiDauId == tran.GiaiDauId &&
                t.KhungGioId == khungGioId &&
                t.NgayThiDau.Date == ngay.Date);
            if (trungLich) return (false, "Khung giờ này đã có trận khác trong cùng ngày!", null, default);

            tran.KhungGioId = khungGioId;
            tran.NgayThiDau = ngay.Date;
            await _context.SaveChangesAsync();
            return (true, "", kg, ngay);
        }

        // ══════════════════════════════════════════════════════════
        // Sinh vòng knock-out (delegate)
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> SinhVongKnockOut(int giaiId, int ownerId)
        {
            var giai = await _context.GiaiDaus
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);
            if (giai == null) return (false, "Không tìm thấy giải!");
            if (giai.TrangThai != "Active")
                return (false, "Chỉ sinh knock-out khi giải đang Active!");

            var tranBangChuaXong = await _context.TranDaus
                .CountAsync(t => t.GiaiDauId == giaiId
                              && t.LoaiVong == "VongBang"
                              && t.TrangThai != "Closed");
            if (tranBangChuaXong > 0)
                return (false, $"Còn {tranBangChuaXong} trận vòng bảng chưa kết thúc!");

            return await _knockOutService.SinhVongKnockOut(giaiId);
        }

        // ══════════════════════════════════════════════════════════
        // Xử lý sự cố: đội bỏ cuộc → xử thua 0-3
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> XuLySuCo(
            int tranDauId, int doiBoCuocId, string lyDo, int ownerId)
        {
            var tran = await _context.TranDaus
                .Include(t => t.GiaiDau)
                .Include(t => t.DoiNha)
                .Include(t => t.DoiKhach)
                .FirstOrDefaultAsync(t => t.Id == tranDauId && t.GiaiDau.OwnerId == ownerId);

            if (tran == null) return (false, "Không tìm thấy trận!");
            if (tran.TrangThai == "Closed") return (false, "Trận đã kết thúc!");
            if (tran.DoiNhaId != doiBoCuocId && tran.DoiKhachId != doiBoCuocId)
                return (false, "Đội không tham gia trận này!");
            if (string.IsNullOrWhiteSpace(lyDo))
                return (false, "Phải nhập lý do xử lý sự cố!");

            bool doiNhaBoCuoc = doiBoCuocId == tran.DoiNhaId;
            tran.BanThangNha = doiNhaBoCuoc ? 0 : 3;
            tran.BanThangKhach = doiNhaBoCuoc ? 3 : 0;
            tran.TrangThai = "Closed";

            _context.SuKienTrans.Add(new SuKienTran
            {
                TranDauId = tranDauId,
                DoiId = doiBoCuocId,
                LoaiSuKien = "SuCo",
                GhiChu = $"Bỏ cuộc. Lý do: {lyDo}",
                ThoiGianGhi = DateTime.Now
            });

            var doi = await _context.DoiBongs.FindAsync(doiBoCuocId);
            if (doi != null) doi.TienKyQuyConLai = 0;

            await _context.SaveChangesAsync();

            await _suspensionService.XuLyTreoGio(tran.GiaiDauId);
            await _knockOutService.CapNhatDoiKnockOut(tranDauId);

            // Realtime broadcast
            try
            {
                await TournamentHub.BroadcastTyso(_hubContext, tran.GiaiDauId, new TysoDto
                {
                    TranDauId = tranDauId,
                    TysoNha = tran.BanThangNha ?? 0,
                    TysoKhach = tran.BanThangKhach ?? 0,
                    TenNha = tran.DoiNha?.TenDoi ?? "",
                    TenKhach = tran.DoiKhach?.TenDoi ?? ""
                });
                await TournamentHub.BroadcastTranKetThuc(_hubContext, tran.GiaiDauId, tranDauId);
                var bxh = await _standingService.GetStandings(tran.GiaiDauId);
                await TournamentHub.BroadcastBXH(_hubContext, tran.GiaiDauId, bxh);
            }
            catch { }

            return (true, "");
        }

        // ── Helper ─────────────────────────────────────────────
        private Task<GiaiDau?> LayGiaiCuaOwner(int giaiId, int ownerId)
            => _context.GiaiDaus.FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);
    }

    // ── DTOs ─────────────────────────────────────────────────────
    public class CreateGiaiDauDto
    {
        public string TenGiai { get; set; } = "";
        public string? MoTa { get; set; }
        public int SanBongId { get; set; }
        public int SoDoiToiDa { get; set; } = 8;
        public int SoBang { get; set; } = 2;
        public decimal LePhiGiai { get; set; }
        public decimal TienKyQuy { get; set; }
        public decimal TienPhatTheVang { get; set; } = 20000m;
        public decimal TienPhatTheDo { get; set; } = 100000m;
        public int SoTranTreoGioTheDo { get; set; } = 1;
        public int SoTheVangTichLuy { get; set; } = 2;
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public DateTime? ThoiGianDong { get; set; }

        // Lịch slot Owner đã block (FullCalendar) — gửi dưới dạng JSON string
        public string? LichBlockJson { get; set; }

        public List<ScheduleService.SlotKhungGio>? LichBlock
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LichBlockJson)) return null;
                try
                {
                    return JsonSerializer.Deserialize<List<ScheduleService.SlotKhungGio>>(
                        LichBlockJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { return null; }
            }
        }
    }
}
