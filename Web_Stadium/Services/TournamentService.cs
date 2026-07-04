using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web_Stadium.EFCore;
using Web_Stadium.Hubs;
using Web_Stadium.Services.JavaClient;

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
        private readonly TournamentSchedulerClient _schedulerClient;
        private readonly IServiceScopeFactory _scopeFactory;

        public TournamentService(
            SanBongContext context,
            ScheduleService scheduleService,
            StandingService standingService,
            SuspensionService suspensionService,
            TournamentNotificationService notificationService,
            KnockOutService knockOutService,
            IHubContext<TournamentHub> hubContext,
            TournamentSchedulerClient schedulerClient,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _scheduleService = scheduleService;
            _standingService = standingService;
            _suspensionService = suspensionService;
            _notificationService = notificationService;
            _knockOutService = knockOutService;
            _hubContext = hubContext;
            _schedulerClient = schedulerClient;
            _scopeFactory = scopeFactory;
        }

        // ══════════════════════════════════════════════════════════
        // Chạy email trong scope DI riêng — tránh dùng chung _context
        // với request hiện tại (gây "A second operation was started on
        // this context instance" khi controller còn thao tác DB sau đó).
        // ══════════════════════════════════════════════════════════
        private void FireAndForgetEmail(Func<TournamentNotificationService, Task> action)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var notif = scope.ServiceProvider
                        .GetRequiredService<TournamentNotificationService>();
                    await action(notif);
                }
                catch { /* fire & forget */ }
            });
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
                AutoMode = dto.AutoMode,
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

            // Email xác nhận (fire & forget — scope riêng để không đụng _context của request)
            var doiIdCopy = doiId;
            FireAndForgetEmail(svc => svc.GuiEmailXacNhanDangKy(doiIdCopy));

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
        // Mở đăng ký: Approved → RegistrationOpen
        // Yêu cầu Admin phê duyệt trước (Draft → Approved). Owner không được
        // tự mở đăng ký một giải Draft — tránh né khâu kiểm duyệt.
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> MoiDangKy(int giaiId, int ownerId)
        {
            var giai = await LayGiaiCuaOwner(giaiId, ownerId);
            if (giai == null) return (false, "Không tìm thấy giải!");

            if (giai.TrangThai == "Draft")
                return (false, "Giải chưa được Admin phê duyệt! Vui lòng đợi phê duyệt trước khi mở đăng ký.");
            if (giai.TrangThai != "Approved")
                return (false, "Chỉ mở đăng ký khi giải đã được phê duyệt (trạng thái Approved)!");

            giai.TrangThai = "RegistrationOpen";
            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // Đóng đăng ký: RegistrationOpen → RegistrationClosed
        // Nếu giải bật AutoMode → chạy tiếp pipeline tự động (chia bảng
        // + xếp lịch + commit sang Active). Thất bại giữ RegistrationClosed
        // để owner làm tay bằng luồng XemTruocLich cũ.
        // daTuDongChot=true khi pipeline auto commit thành công → controller
        // redirect thẳng về Details thay vì ChiaBang.
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error, bool daTuDongChot)> DongDangKy(int giaiId, int ownerId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.DoiBongs)
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);

            if (giai == null) return (false, "Không tìm thấy giải!", false);
            if (giai.TrangThai != "RegistrationOpen")
                return (false, "Chỉ đóng đăng ký khi giải đang mở!", false);

            var soDoiHopLe = giai.DoiBongs.Count(d => d.DaThanhToan);
            if (soDoiHopLe < giai.SoDoiToiDa)
            {
                var soDoiChuaTT = giai.DoiBongs.Count(d => !d.DaThanhToan);
                var conThieu = giai.SoDoiToiDa - soDoiHopLe;
                var msg = $"Chưa đủ đội để đóng đăng ký! Cần đủ {giai.SoDoiToiDa} đội đã thanh toán " +
                          $"(hiện có {soDoiHopLe} đội đã thanh toán";
                if (soDoiChuaTT > 0) msg += $", {soDoiChuaTT} đội chờ xác nhận";
                msg += $"). Còn thiếu {conThieu} đội.";
                return (false, msg, false);
            }

            giai.TrangThai = "RegistrationClosed";
            giai.ThoiGianDongDanhSach = DateTime.Now;
            await _context.SaveChangesAsync();

            if (!giai.AutoMode) return (true, "", false);

            // AutoMode: cố gắng chạy pipeline. Nếu fail thì đã có email + audit
            // log bên trong; giữ RegistrationClosed để owner xử lý tay.
            var (autoOk, autoErr) = await ChayAutoPipelineAsync(giaiId, ownerId);
            return (true, autoOk ? "" : autoErr, autoOk);
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
        // CHỐT LỊCH — commit sau khi owner tinh chỉnh ở màn XemTruocLich.
        // finalAssignments: virtual matchId → {khungGioId, ngay yyyy-MM-dd}.
        // Optimistic concurrency: re-check DatSans hiện tại; nếu slot đã có
        // booking khác chèn vào trong lúc owner ngồi kéo thả → báo lỗi.
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error)> ChotLichAsync(
            int giaiId, int ownerId, List<ChotLichAssignmentDto> finalAssignments)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs).ThenInclude(d => d.Bang)
                .Include(g => g.DoiBongs).ThenInclude(d => d.DoiTruong)
                .Include(g => g.SanBong).ThenInclude(s => s.KhungGios)
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);

            if (giai == null) return (false, "Không tìm thấy giải!");
            if (giai.TrangThai != "RegistrationClosed")
                return (false, "Chỉ chốt lịch khi giải ở trạng thái Đóng đăng ký!");

            var doiChuaBang = giai.DoiBongs.Where(d => d.BangId == null && d.DaThanhToan).ToList();
            if (doiChuaBang.Any())
                return (false, $"Còn {doiChuaBang.Count} đội chưa được xếp bảng!");

            return await CommitLichAsync(giai, ownerId, finalAssignments);
        }

        // ══════════════════════════════════════════════════════════
        // Helper: sinh Berger + gán slot theo assignments + verify không
        // xung đột + commit TranDau + DummyBooking + set Active + gửi
        // email/broadcast. Dùng chung cho ChotLichAsync (manual) và
        // ChayAutoPipelineAsync (auto). Caller đã bảo đảm giai != null,
        // TrangThai == "RegistrationClosed", các đội đã có Bang.
        // ══════════════════════════════════════════════════════════
        private async Task<(bool ok, string error)> CommitLichAsync(
            GiaiDau giai, int ownerId, List<ChotLichAssignmentDto> finalAssignments)
        {
            // 1) Sinh trận Berger (cùng logic như XemTruocLich để giữ mapping matchId virtual)
            var tranDaus = _scheduleService.SinhLichVongTron(giai, lichBlock: null);
            if (tranDaus.Count == 0)
                return (false, "Không sinh được trận đấu!");

            // 2) Map assignments client-side → gán vào TranDau
            var assignMap = finalAssignments.ToDictionary(a => a.MatchId);
            for (int i = 0; i < tranDaus.Count; i++)
            {
                if (!assignMap.TryGetValue(i, out var a))
                    return (false, $"Trận #{i + 1} chưa được gán slot — hãy xếp đủ trước khi chốt!");

                if (!DateTime.TryParse(a.Ngay, out var ngay))
                    return (false, $"Ngày không hợp lệ ở trận #{i + 1}!");

                tranDaus[i].KhungGioId = a.KhungGioId;
                tranDaus[i].NgayThiDau = ngay.Date;
            }

            // 3) Optimistic concurrency: verify slot chưa bị chiếm bởi booking khác
            var khungIds = tranDaus.Select(t => t.KhungGioId!.Value).ToList();
            var ngayBD = giai.NgayBatDau.Date;
            var ngayKT = giai.NgayKetThuc.Date;
            var busySlots = await _context.DatSans
                .Where(d => d.NgayThiDau >= ngayBD && d.NgayThiDau <= ngayKT
                         && d.TrangThai != "DaHuy"
                         && d.GiaiDauId != giai.Id
                         && khungIds.Contains(d.KhungGioId))
                .Select(d => new { d.KhungGioId, d.NgayThiDau })
                .ToListAsync();

            foreach (var t in tranDaus)
            {
                if (busySlots.Any(b => b.KhungGioId == t.KhungGioId
                                    && b.NgayThiDau.Date == t.NgayThiDau.Date))
                    return (false, "Có slot vừa bị khách đặt trong lúc bạn xếp lịch! Hãy tải lại trang và xếp lại.");
            }

            // 4) Commit: TranDau + DummyBooking (mỗi slot đúng 1 dummy, dedup theo (khungGio, ngày))
            _context.TranDaus.AddRange(tranDaus);
            await _context.SaveChangesAsync();

            var dedupSlots = tranDaus
                .GroupBy(t => new { t.KhungGioId, t.NgayThiDau.Date })
                .Select(g => new { g.Key.KhungGioId, Ngay = g.Key.Date })
                .ToList();

            foreach (var s in dedupSlots)
            {
                _context.DatSans.Add(new DatSan
                {
                    UserId = ownerId,
                    KhungGioId = s.KhungGioId!.Value,
                    NgayThiDau = s.Ngay,
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

            giai.TrangThai = "Active";
            await _context.SaveChangesAsync();

            // Email lịch đấu (fire & forget — scope riêng)
            var giaiIdCopy = giai.Id;
            FireAndForgetEmail(svc => svc.GuiEmailLichDau(giaiIdCopy));

            // Realtime: thông báo giải bắt đầu
            try
            {
                var bxh = await _standingService.GetStandings(giai.Id);
                await TournamentHub.BroadcastBXH(_hubContext, giai.Id, bxh);
            }
            catch { }

            return (true, "");
        }

        // ══════════════════════════════════════════════════════════
        // AUTO PIPELINE — gọi khi giải bật AutoMode + vừa Đóng đăng ký:
        //   1) Java /draw   → BangId cho mỗi đội
        //   2) Java /schedule → assignments cho từng trận Berger
        //   3) CommitLichAsync → TranDau + DummyBooking + Active + email
        // Fail bất cứ bước nào → rollback (bỏ BangId đã gán), giữ trạng thái
        // RegistrationClosed để owner làm tay bằng luồng XemTruocLich cũ,
        // ghi AuditLog + gửi email cảnh báo.
        // ══════════════════════════════════════════════════════════
        private async Task<(bool ok, string error)> ChayAutoPipelineAsync(int giaiId, int ownerId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs).ThenInclude(d => d.Bang)
                .Include(g => g.DoiBongs).ThenInclude(d => d.DoiTruong)
                .Include(g => g.SanBong).ThenInclude(s => s!.KhungGios)
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);

            if (giai == null) return (false, "Không tìm thấy giải!");

            // ── Bước 1: chia bảng qua Java /draw ─────────────────────
            var teamsPaid = giai.DoiBongs.Where(d => d.DaThanhToan).ToList();
            var bangSorted = giai.BangDaus.OrderBy(b => b.Id).ToList();
            if (bangSorted.Count == 0)
                return await AutoFail(giaiId, ownerId, giai, teamsPaid,
                    "Giải chưa có bảng đấu — không thể chia tự động!",
                    new List<string> { "Thiếu BangDau" });

            DrawResponse drawResp;
            try
            {
                drawResp = await _schedulerClient.DrawGroupsAsync(new DrawRequest
                {
                    GiaiId = giai.Id,
                    SoBang = bangSorted.Count,
                    Teams = teamsPaid.Select(d => new TeamDto
                    {
                        Id = d.Id,
                        TenDoi = d.TenDoi,
                        SeedRating = 0
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return await AutoFail(giaiId, ownerId, giai, teamsPaid,
                    "Không kết nối được Java /draw: " + ex.Message,
                    new List<string> { ex.Message });
            }

            var doiMap = teamsPaid.ToDictionary(d => d.Id);
            foreach (var a in drawResp.Assignments)
            {
                if (!doiMap.TryGetValue(a.TeamId, out var doi)) continue;
                if (a.GroupIndex < 0 || a.GroupIndex >= bangSorted.Count) continue;
                doi.BangId = bangSorted[a.GroupIndex].Id;
            }
            await _context.SaveChangesAsync();

            await GhiAuditLogAsync(ownerId, "AutoChiaBang", giai.Id,
                $"Auto chia {teamsPaid.Count} đội vào {bangSorted.Count} bảng " +
                $"({string.Join(", ", bangSorted.Select(b => b.TenBang + ": " + doiMap.Values.Count(d => d.BangId == b.Id)))})");

            // ── Bước 2: sinh Berger + gọi Java /schedule ────────────
            var tranRaw = _scheduleService.SinhLichVongTron(giai, lichBlock: null);
            if (tranRaw.Count == 0)
                return await AutoFail(giaiId, ownerId, giai, teamsPaid,
                    "Không sinh được trận đấu (bảng đấu thiếu đội)!",
                    new List<string> { "SinhLichVongTron trả về rỗng" });

            var matchesById = new Dictionary<int, TranDau>();
            for (int i = 0; i < tranRaw.Count; i++) matchesById[i] = tranRaw[i];

            List<ScheduleService.SlotKhungGio> lichBlock = new();
            if (!string.IsNullOrEmpty(giai.LichBlockJson))
            {
                try
                {
                    lichBlock = JsonSerializer.Deserialize<List<ScheduleService.SlotKhungGio>>(
                        giai.LichBlockJson, _jsonOpts) ?? new();
                }
                catch { lichBlock = new(); }
            }
            if (lichBlock.Count == 0)
                return await AutoFail(giaiId, ownerId, giai, teamsPaid,
                    "Chưa có slot block! Vào 'Lịch block' chọn slot trước khi bật AutoMode.",
                    new List<string> { "LichBlockJson rỗng" });

            var khungGioMap = giai.SanBong!.KhungGios.ToDictionary(k => k.Id);
            var availableSlots = lichBlock
                .Where(s => khungGioMap.ContainsKey(s.KhungGioId))
                .Select(s => new SlotDto
                {
                    KhungGioId = s.KhungGioId,
                    Ngay = s.Ngay.ToString("yyyy-MM-dd"),
                    GioBatDau = khungGioMap[s.KhungGioId].GioBatDau.ToString(@"HH\:mm"),
                    GioKetThuc = khungGioMap[s.KhungGioId].GioKetThuc.ToString(@"HH\:mm")
                })
                .ToList();

            var khungIds = khungGioMap.Keys.ToList();
            var ngayBD = giai.NgayBatDau.Date;
            var ngayKT = giai.NgayKetThuc.Date;
            var bookingConflicts = await _context.DatSans
                .Where(d => d.NgayThiDau >= ngayBD && d.NgayThiDau <= ngayKT
                         && d.TrangThai != "DaHuy"
                         && d.GiaiDauId != giai.Id
                         && khungIds.Contains(d.KhungGioId))
                .Select(d => new BookingConflictDto
                {
                    KhungGioId = d.KhungGioId,
                    Ngay = d.NgayThiDau.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            var bangMap = giai.BangDaus.ToDictionary(b => b.Id);
            var matchesDto = matchesById.Select(kv =>
            {
                var t = kv.Value;
                // Berger sinh cho VongBang nên 2 team luôn có Id (fallback 0 chỉ để compile-safe).
                return new MatchDto
                {
                    MatchId = kv.Key,
                    TeamA = t.DoiNhaId ?? 0,
                    TeamB = t.DoiKhachId ?? 0,
                    TeamAName = t.DoiNhaId.HasValue && doiMap.TryGetValue(t.DoiNhaId.Value, out var da) ? da.TenDoi : "?",
                    TeamBName = t.DoiKhachId.HasValue && doiMap.TryGetValue(t.DoiKhachId.Value, out var db) ? db.TenDoi : "?",
                    Round = t.VongDau,
                    GroupId = t.BangId ?? 0,
                    GroupName = t.BangId.HasValue && bangMap.ContainsKey(t.BangId.Value)
                        ? bangMap[t.BangId.Value].TenBang : "-"
                };
            }).ToList();

            ScheduleResponse resp;
            try
            {
                resp = await _schedulerClient.SolveAsync(new ScheduleRequest
                {
                    GiaiId = giaiId,
                    Matches = matchesDto,
                    AvailableSlots = availableSlots,
                    Bookings = bookingConflicts,
                    Constraints = new ConstraintsDto()
                });
            }
            catch (Exception ex)
            {
                return await AutoFail(giaiId, ownerId, giai, teamsPaid,
                    "Không kết nối được Java /schedule: " + ex.Message,
                    new List<string> { ex.Message });
            }

            if (resp.Unassigned.Count > 0 || resp.Assignments.Count < matchesById.Count)
            {
                var msg = $"Solver không xếp đủ lịch: {resp.Assignments.Count}/{matchesById.Count} trận.";
                return await AutoFail(giaiId, ownerId, giai, teamsPaid, msg, resp.Warnings);
            }

            // ── Bước 3: build ChotLichAssignmentDto và commit ───────
            var chotDto = resp.Assignments.Select(a => new ChotLichAssignmentDto
            {
                MatchId = a.MatchId,
                KhungGioId = a.KhungGioId,
                Ngay = a.Ngay
            }).ToList();

            var (ok, err) = await CommitLichAsync(giai, ownerId, chotDto);
            if (!ok)
                return await AutoFail(giaiId, ownerId, giai, teamsPaid, err, resp.Warnings);

            await GhiAuditLogAsync(ownerId, "AutoChotLich", giai.Id,
                $"Auto chốt lịch: {matchesById.Count} trận, {resp.Assignments.Count} slot" +
                (resp.Warnings.Count > 0 ? $" (cảnh báo: {string.Join(" | ", resp.Warnings)})" : ""));

            return (true, "");
        }

        // Rollback + audit + email khi auto pipeline hỏng ở bước nào đó.
        private async Task<(bool ok, string error)> AutoFail(
            int giaiId, int ownerId, GiaiDau giai, List<DoiBong> teamsPaid,
            string reason, List<string> warnings)
        {
            // Rollback BangId đã gán (nếu có) — teamsPaid được load từ DB nên EF sẽ update.
            foreach (var d in teamsPaid) d.BangId = null;
            try { await _context.SaveChangesAsync(); } catch { /* best effort */ }

            await GhiAuditLogAsync(ownerId, "AutoPipelineFailed", giaiId,
                reason + (warnings.Count > 0 ? " | warnings: " + string.Join(" | ", warnings) : ""));

            FireAndForgetEmail(svc => svc.GuiEmailAutoPipelineThatBai(giaiId, reason, warnings));

            return (false, reason + " Giữ trạng thái Đóng đăng ký, mời bạn vào 'Xem trước lịch' hoàn tất tay.");
        }

        // Ghi AuditLog (không truyền IpAddress vì gọi từ service, không có HttpContext).
        private async Task GhiAuditLogAsync(int userId, string hanhDong, int doiTuongId, string moTa)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                VaiTro = "Owner",
                HanhDong = hanhDong,
                DoiTuong = "GiaiDau",
                DoiTuongId = doiTuongId,
                MoTa = moTa,
                ThoiGian = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // XEM TRƯỚC LỊCH — sinh trận (Berger) + gọi Java CSP solver,
        // KHÔNG ghi DB. Owner sẽ tinh chỉnh và bấm Chốt.
        // ══════════════════════════════════════════════════════════
        public async Task<(bool ok, string error, PreviewLichResult? preview)>
            XemTruocLichAsync(int giaiId, int ownerId)
        {
            var giai = await _context.GiaiDaus
                .Include(g => g.BangDaus)
                .Include(g => g.DoiBongs).ThenInclude(d => d.Bang)
                .Include(g => g.SanBong).ThenInclude(s => s!.KhungGios)
                .FirstOrDefaultAsync(g => g.Id == giaiId && g.OwnerId == ownerId);

            if (giai == null) return (false, "Không tìm thấy giải!", null);
            if (giai.TrangThai != "RegistrationClosed")
                return (false, "Chỉ xem trước lịch sau khi đóng đăng ký!", null);

            var doiChuaBang = giai.DoiBongs.Where(d => d.BangId == null && d.DaThanhToan).ToList();
            if (doiChuaBang.Any())
                return (false, $"Còn {doiChuaBang.Count} đội chưa được xếp bảng!", null);

            // 1) Berger sinh cặp đấu — KHÔNG gán slot
            var tranRaw = _scheduleService.SinhLichVongTron(giai, lichBlock: null);
            if (tranRaw.Count == 0)
                return (false, "Không sinh được trận đấu (bảng đấu thiếu đội)!", null);

            // Virtual matchId = index trong list (chưa lưu DB nên chưa có Id thật)
            var matchesById = new Dictionary<int, TranDau>();
            for (int i = 0; i < tranRaw.Count; i++) matchesById[i] = tranRaw[i];

            // 2) Available slots từ LichBlockJson × KhungGio
            List<ScheduleService.SlotKhungGio> lichBlock = new();
            if (!string.IsNullOrEmpty(giai.LichBlockJson))
            {
                try
                {
                    lichBlock = JsonSerializer.Deserialize<List<ScheduleService.SlotKhungGio>>(
                        giai.LichBlockJson, _jsonOpts) ?? new();
                }
                catch { lichBlock = new(); }
            }
            if (lichBlock.Count == 0)
                return (false, "Chưa có slot block! Vào 'Lịch block' chọn slot trước.", null);

            var khungGioMap = giai.SanBong!.KhungGios.ToDictionary(k => k.Id);
            var availableSlots = lichBlock
                .Where(s => khungGioMap.ContainsKey(s.KhungGioId))
                .Select(s => new SlotDto
                {
                    KhungGioId = s.KhungGioId,
                    Ngay = s.Ngay.ToString("yyyy-MM-dd"),
                    GioBatDau = khungGioMap[s.KhungGioId].GioBatDau.ToString(@"HH\:mm"),
                    GioKetThuc = khungGioMap[s.KhungGioId].GioKetThuc.ToString(@"HH\:mm")
                })
                .ToList();

            // 3) Booking thường trong khoảng giải, tại khung giờ của sân (không tính chính giải này)
            var khungIds = khungGioMap.Keys.ToList();
            var ngayBD = giai.NgayBatDau.Date;
            var ngayKT = giai.NgayKetThuc.Date;
            var bookingConflicts = await _context.DatSans
                .Where(d => d.NgayThiDau >= ngayBD && d.NgayThiDau <= ngayKT
                         && d.TrangThai != "DaHuy"
                         && d.GiaiDauId != giai.Id
                         && khungIds.Contains(d.KhungGioId))
                .Select(d => new BookingConflictDto
                {
                    KhungGioId = d.KhungGioId,
                    Ngay = d.NgayThiDau.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            // 4) DTO trận cho Java (kèm tên đội để build cảnh báo)
            var doiMap = giai.DoiBongs.ToDictionary(d => d.Id);
            var bangMap = giai.BangDaus.ToDictionary(b => b.Id);

            var matchesDto = matchesById.Select(kv =>
            {
                var t = kv.Value;
                var bang = t.BangId.HasValue && bangMap.ContainsKey(t.BangId.Value)
                    ? bangMap[t.BangId.Value] : null;
                return new MatchDto
                {
                    MatchId = kv.Key,
                    TeamA = t.DoiNhaId ?? 0,
                    TeamB = t.DoiKhachId ?? 0,
                    TeamAName = t.DoiNhaId.HasValue && doiMap.TryGetValue(t.DoiNhaId.Value, out var da) ? da.TenDoi : "?",
                    TeamBName = t.DoiKhachId.HasValue && doiMap.TryGetValue(t.DoiKhachId.Value, out var db) ? db.TenDoi : "?",
                    Round = t.VongDau,
                    GroupId = t.BangId ?? 0,
                    GroupName = bang?.TenBang ?? "-"
                };
            }).ToList();

            // 5) Gọi Java solver
            ScheduleResponse resp;
            try
            {
                resp = await _schedulerClient.SolveAsync(new ScheduleRequest
                {
                    GiaiId = giaiId,
                    Matches = matchesDto,
                    AvailableSlots = availableSlots,
                    Bookings = bookingConflicts,
                    Constraints = new ConstraintsDto() // default: minRest=1, forbidSame=true, preferWeekend=true
                });
            }
            catch (Exception ex)
            {
                return (false, "Không kết nối được Java scheduler: " + ex.Message, null);
            }

            return (true, "", new PreviewLichResult
            {
                Giai = giai,
                MatchesRaw = matchesById,
                MatchesDto = matchesDto,
                AvailableSlots = availableSlots,
                Bookings = bookingConflicts,
                Assignments = resp.Assignments,
                Unassigned = resp.Unassigned,
                Warnings = resp.Warnings
            });
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

        // Owner tick → sau khi Đóng đăng ký sẽ tự chia bảng + xếp lịch (Java).
        public bool AutoMode { get; set; } = false;

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
