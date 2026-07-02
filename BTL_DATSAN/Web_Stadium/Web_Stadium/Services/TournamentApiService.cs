using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Web_Stadium.EFCore;

namespace Web_Stadium.Services
{
    /// <summary>
    /// Lop trung gian duy nhat goi sang tournament-service (Java, port 8080).
    /// 4 Tournament Controller KHONG goi HttpClient truc tiep — luon di qua day.
    ///
    /// Nhiem vu:
    ///  - Dinh kem JWT (tu Request.Cookies["jwt"]) vao header Authorization.
    ///  - Goi API Java, doc JSON (ApiResponse{success,message,data,timestamp}).
    ///  - Map DTO JSON (camelCase, phang) sang dung cac lop EFCore hien co
    ///    (GiaiDau, DoiBong, TranDau, BangDau, ThanhVienDoi, User, SanBong)
    ///    de Views (.cshtml) KHONG phai sua gi ca — chung van bind y het nhu
    ///    khi du lieu do EF Core query truc tiep.
    ///  - Neu Java app khong chay / timeout → bat exception, log, tra ve
    ///    null/rong thay vi de loi 500 lam sap trang.
    /// </summary>
    public class TournamentApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TournamentApiService> _logger;

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public TournamentApiService(IHttpClientFactory httpClientFactory, ILogger<TournamentApiService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>Lay JWT tu cookie — goi tu Controller truoc khi goi cac method can dang nhap.</summary>
        public string? GetJwtFromContext(HttpContext context) => context.Request.Cookies["jwt"];

        // ════════════════════════════════════════════════════════════
        // PUBLIC — khong can token
        // ════════════════════════════════════════════════════════════

        public async Task<List<GiaiDau>> GetDanhSachGiai(
            string? keyword, string? quan, string? trangThai,
            decimal? lePhiTu, decimal? lePhiDen, string? sapXep)
        {
            var query = BuildQuery(new()
            {
                ["keyword"] = keyword,
                ["quan"] = quan,
                ["trangThai"] = trangThai,
                ["lePhiTu"] = lePhiTu?.ToString(),
                ["lePhiDen"] = lePhiDen?.ToString(),
                ["sapXep"] = sapXep
            });

            var list = await GetAsync<List<GiaiDauApiDto>>($"public/tournament{query}", null);
            return (list ?? new()).Select(ToEfGiaiDau).ToList();
        }

        public async Task<GiaiDau?> GetChiTietGiai(int id)
        {
            var dto = await GetAsync<GiaiDauApiDto>($"public/tournament/{id}", null);
            return dto == null ? null : ToEfGiaiDau(dto);
        }

        public async Task<Dictionary<int, List<StandingRow>>> GetBangXepHang(int id)
        {
            var dto = await GetAsync<Dictionary<int, List<StandingRowApiDto>>>($"public/tournament/{id}/standings", null);
            if (dto == null) return new();
            return dto.ToDictionary(kv => kv.Key, kv => kv.Value.Select(ToStandingRow).ToList());
        }

        public async Task<List<TranDau>> GetLichThiDau(int id)
        {
            var list = await GetAsync<List<TranDauApiDto>>($"public/tournament/{id}/matches", null);
            return (list ?? new()).Select(ToEfTranDau).ToList();
        }

        public async Task<List<ThanhVienDoi>> GetVuaPhaLuoi(int id)
        {
            var list = await GetAsync<List<ThanhVienDoiApiDto>>($"public/tournament/{id}/top-scorers", null);
            return (list ?? new()).Select(ToEfThanhVien).ToList();
        }

        // ── Dang ky doi / nop danh sach (can dang nhap, khong phan biet role) ──

        public async Task<(bool ok, string? error, DoiBong? doi)> DangKyDoi(int giaiId, string tenDoi, string jwtToken)
        {
            var body = new { tenDoi };
            var (resp, error) = await PostAsync<DoiBongApiDto>($"public/tournament/{giaiId}/register", body, jwtToken);
            return resp == null ? (false, error, null) : (true, null, ToEfDoiBong(resp));
        }

        public async Task<DoiBong?> GetDoiCuaToi(int doiId, string jwtToken)
        {
            var dto = await GetAsync<DoiBongApiDto>($"public/tournament/teams/{doiId}", jwtToken);
            return dto == null ? null : ToEfDoiBong(dto);
        }

        public async Task<List<ThanhVienDoi>> GetRoster(int doiId, string jwtToken)
        {
            var list = await GetAsync<List<ThanhVienDoiApiDto>>($"public/tournament/teams/{doiId}/roster", jwtToken);
            return (list ?? new()).Select(ToEfThanhVien).ToList();
        }

        public async Task<(bool ok, string? error)> ThemThanhVien(int doiId, string hoTen, int soAo, string? anhDaiDien, string jwtToken)
        {
            var body = new { hoTen, soAo, anhDaiDien };
            var (resp, error) = await PostAsync<ThanhVienDoiApiDto>($"public/tournament/teams/{doiId}/members", body, jwtToken);
            return resp == null ? (false, error) : (true, null);
        }

        public async Task<(bool ok, string? error)> XoaThanhVien(int doiId, int thanhVienId, string jwtToken)
        {
            return await DeleteAsync($"public/tournament/teams/{doiId}/members/{thanhVienId}", jwtToken);
        }

        // ════════════════════════════════════════════════════════════
        // OWNER — can token, role Owner
        // ════════════════════════════════════════════════════════════

        public async Task<(bool ok, string? error, GiaiDau? giai)> TaoGiai(CreateGiaiDauDto dto, string jwtToken)
        {
            var (resp, error) = await PostAsync<GiaiDauApiDto>("tournament/owner", dto, jwtToken);
            return resp == null ? (false, error, null) : (true, null, ToEfGiaiDau(resp));
        }

        public async Task<List<GiaiDau>> GetMyTournaments(string jwtToken)
        {
            var list = await GetAsync<List<GiaiDauApiDto>>("tournament/owner/my-tournaments", jwtToken);
            return (list ?? new()).Select(ToEfGiaiDau).ToList();
        }

        public async Task<GiaiDau?> GetChiTietGiaiOwner(int id, string jwtToken)
        {
            var dto = await GetAsync<GiaiDauApiDto>($"tournament/owner/{id}", jwtToken);
            return dto == null ? null : ToEfGiaiDau(dto);
        }

        public async Task<(bool ok, string? error)> MoiDangKy(int id, string jwtToken)
            => await PostAsync($"tournament/owner/{id}/open-registration", null, jwtToken);

        public async Task<(bool ok, string? error)> DongDangKy(int id, string jwtToken)
            => await PostAsync($"tournament/owner/{id}/close-registration", null, jwtToken);

        public async Task<(bool ok, string? error)> GanBang(int doiId, int? bangId, string jwtToken)
        {
            var query = BuildQuery(new() { ["bangId"] = bangId?.ToString() });
            return await PostAsync($"tournament/owner/teams/{doiId}/assign-group{query}", null, jwtToken);
        }

        public async Task<(bool ok, string? error)> KhoiTao(int id, string jwtToken)
            => await PostAsync($"tournament/owner/{id}/start", null, jwtToken);

        public async Task<(bool ok, string? error)> KetThuc(int id, string jwtToken)
            => await PostAsync($"tournament/owner/{id}/finish", null, jwtToken);

        public async Task<(bool ok, string? error)> XuLySuCo(int tranDauId, int doiBoCuocId, string lyDo, string jwtToken)
        {
            var query = BuildQuery(new() { ["doiBoCuocId"] = doiBoCuocId.ToString(), ["lyDo"] = lyDo });
            return await PostAsync($"tournament/owner/matches/{tranDauId}/incident{query}", null, jwtToken);
        }

        public async Task<List<DoiBong>> GetDanhSachDoi(int giaiId, string jwtToken)
        {
            var list = await GetAsync<List<DoiBongApiDto>>($"tournament/owner/{giaiId}/teams", jwtToken);
            return (list ?? new()).Select(ToEfDoiBong).ToList();
        }

        public async Task<(bool ok, string? error)> XacNhanThanhToan(int giaiId, int doiId, string jwtToken)
            => await PostAsync($"tournament/owner/{giaiId}/teams/{doiId}/confirm-payment", null, jwtToken);

        public async Task<byte[]?> GetExcelDoiSoat(int id, string jwtToken)
            => await GetBytesAsync($"tournament/owner/{id}/excel", jwtToken);

        // ══════════════════════════════════════════════════════════
        // KNOCK-OUT — Java: /tournament/owner/{id}/generate-knockout + /bracket
        // ══════════════════════════════════════════════════════════

        // (bool ok, string? error) thay vi Task suong — giu dung pattern loi cua
        // MoiDangKy/DongDangKy/KhoiTao... de Controller co the TempData["Error"]
        // khi Java tra loi (VD: "Con N tran vong bang chua ket thuc!").
        public async Task<(bool ok, string? error)> SinhVongKnockOut(int giaiId, string jwtToken)
            => await PostAsync($"tournament/owner/{giaiId}/generate-knockout", null, jwtToken);

        // Tra ve object (JsonElement) dung nhu yeu cau — KHONG dung de dung Bracket.cshtml
        // (view can GiaiDau day du voi DoiBongs/TranDaus, xem TournamentController.Bracket()),
        // giu lai cho truong hop can du lieu bracket thuan (VD: AJAX rieng sau nay).
        public async Task<object?> GetBracket(int giaiId, string jwtToken)
            => await GetAsync<object>($"tournament/owner/{giaiId}/bracket", jwtToken);

        // ════════════════════════════════════════════════════════════
        // STAFF — can token, role Staff
        // ════════════════════════════════════════════════════════════

        public async Task<List<TranDau>> GetTranDauHomNay(string? loc, string? trangThai, string jwtToken)
        {
            var query = BuildQuery(new() { ["loc"] = loc, ["trangThai"] = trangThai });
            var list = await GetAsync<List<TranDauApiDto>>($"tournament/staff/matches{query}", jwtToken);
            return (list ?? new()).Select(ToEfTranDau).ToList();
        }

        public async Task<TranDau?> GetChiTietTran(int matchId, string jwtToken)
        {
            var dto = await GetAsync<TranDauApiDto>($"tournament/staff/matches/{matchId}", jwtToken);
            return dto == null ? null : ToEfTranDau(dto);
        }

        public async Task<(bool ok, string? error, TranDau? tran)> CheckIn(int matchId, string jwtToken)
        {
            var (resp, error) = await PostAsync<TranDauApiDto>($"tournament/staff/matches/{matchId}/checkin", null, jwtToken);
            return resp == null ? (false, error, null) : (true, null, ToEfTranDau(resp));
        }

        public async Task<(bool ok, string? error, string? loaiThucTe, long tysoNha, long tysoKhach)> GhiSuKien(
            int matchId, int? thanhVienId, int doiId, string loaiSuKien, int phut, string? ghiChu, string jwtToken)
        {
            var body = new { thanhVienId, doiId, loaiSuKien, phut, ghiChu };
            var (resp, error) = await PostAsync<EventResultApiDto>($"tournament/staff/matches/{matchId}/events", body, jwtToken);
            return resp == null
                ? (false, error, null, 0, 0)
                : (true, null, resp.LoaiThucTe, resp.TysoNha, resp.TysoKhach);
        }

        public async Task<(long tysoNha, long tysoKhach)> HuyBanThang(int matchId, int suKienId, string jwtToken)
        {
            var (resp, _) = await DeleteAsync<EventResultApiDto>($"tournament/staff/matches/{matchId}/events/{suKienId}", jwtToken);
            return resp == null ? (0, 0) : (resp.TysoNha, resp.TysoKhach);
        }

        public async Task<(bool ok, string? error, int banThangNha, int banThangKhach)> XacNhanKetThuc(int matchId, string jwtToken)
        {
            var (resp, error) = await PostAsync<ConfirmResultApiDto>($"tournament/staff/matches/{matchId}/confirm", null, jwtToken);
            if (resp?.TranDau == null) return (false, error, 0, 0);
            return (true, null, resp.TranDau.BanThangNha ?? 0, resp.TranDau.BanThangKhach ?? 0);
        }

        // ════════════════════════════════════════════════════════════
        // ADMIN — can token, role Admin
        // ════════════════════════════════════════════════════════════

        public async Task<List<GiaiDau>> GetAllGiai(string? trangThai, string? keyword, int? ownerId, string? sapXep, string jwtToken)
        {
            var query = BuildQuery(new()
            {
                ["trangThai"] = trangThai,
                ["keyword"] = keyword,
                ["ownerId"] = ownerId?.ToString(),
                ["sapXep"] = sapXep
            });
            var list = await GetAsync<List<GiaiDauApiDto>>($"admin/tournament{query}", jwtToken);
            return (list ?? new()).Select(ToEfGiaiDau).ToList();
        }

        public async Task<KpiApiDto> GetKpi(string jwtToken)
            => await GetAsync<KpiApiDto>("admin/tournament/kpi", jwtToken) ?? new KpiApiDto();

        public async Task<GiaiDau?> GetChiTietGiaiAdmin(int id, string jwtToken)
        {
            var dto = await GetAsync<GiaiDauApiDto>($"admin/tournament/{id}", jwtToken);
            return dto == null ? null : ToEfGiaiDau(dto);
        }

        public async Task<(bool ok, string? error)> PheDuyet(int id, string? ghiChu, string jwtToken)
        {
            var query = BuildQuery(new() { ["ghiChu"] = ghiChu });
            return await PostAsync($"admin/tournament/{id}/approve{query}", null, jwtToken);
        }

        public async Task<(bool ok, string? error)> TuChoi(int id, string lyDo, string jwtToken)
        {
            var query = BuildQuery(new() { ["lyDo"] = lyDo });
            return await PostAsync($"admin/tournament/{id}/reject{query}", null, jwtToken);
        }

        public async Task<byte[]?> GetExcelDoiSoatAdmin(int id, string jwtToken)
            => await GetBytesAsync($"admin/tournament/{id}/excel", jwtToken);

        public async Task<BaoCaoApiDto> GetBaoCao(string jwtToken)
            => await GetAsync<BaoCaoApiDto>("admin/tournament/report", jwtToken) ?? new BaoCaoApiDto();

        // ════════════════════════════════════════════════════════════
        // HTTP helpers — dung chung, tu bat loi ket noi/timeout
        // ════════════════════════════════════════════════════════════

        private HttpClient CreateClient(string? jwtToken)
        {
            var client = _httpClientFactory.CreateClient("TournamentService");
            if (!string.IsNullOrEmpty(jwtToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            return client;
        }

        private async Task<T?> GetAsync<T>(string path, string? jwtToken)
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.GetAsync(path);
                var envelope = await ReadEnvelope<T>(response);
                if (envelope == null || !envelope.Success)
                {
                    if (envelope != null) _logger.LogWarning("Java API tra loi ({Path}): {Message}", path, envelope.Message);
                    return default;
                }
                return envelope.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc tournament-service (GET {Path})", path);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi tournament-service (GET {Path})", path);
                return default;
            }
        }

        private async Task<byte[]?> GetBytesAsync(string path, string? jwtToken)
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.GetAsync(path);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc tournament-service (GET bytes {Path})", path);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi tournament-service (GET bytes {Path})", path);
                return null;
            }
        }

        private async Task<(bool ok, string? error)> PostAsync(string path, object? body, string? jwtToken)
        {
            var (_, error) = await PostAsync<object>(path, body, jwtToken);
            return (error == null, error);
        }

        private async Task<(T? data, string? error)> PostAsync<T>(string path, object? body, string? jwtToken)
        {
            try
            {
                var client = CreateClient(jwtToken);
                HttpResponseMessage response = body == null
                    ? await client.PostAsync(path, null)
                    : await client.PostAsJsonAsync(path, body, WriteOptions);

                var envelope = await ReadEnvelope<T>(response);
                if (envelope == null) return (default, "Khong ket noi duoc may chu giai dau!");
                if (!envelope.Success) return (default, envelope.Message ?? "Co loi xay ra!");
                return (envelope.Data, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc tournament-service (POST {Path})", path);
                return (default, "Khong ket noi duoc may chu giai dau!");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi tournament-service (POST {Path})", path);
                return (default, "May chu giai dau phan hoi qua cham!");
            }
        }

        private async Task<(bool ok, string? error)> DeleteAsync(string path, string? jwtToken)
        {
            var (_, error) = await DeleteAsync<object>(path, jwtToken);
            return (error == null, error);
        }

        private async Task<(T? data, string? error)> DeleteAsync<T>(string path, string? jwtToken)
        {
            try
            {
                var client = CreateClient(jwtToken);
                var response = await client.DeleteAsync(path);
                var envelope = await ReadEnvelope<T>(response);
                if (envelope == null) return (default, "Khong ket noi duoc may chu giai dau!");
                if (!envelope.Success) return (default, envelope.Message ?? "Co loi xay ra!");
                return (envelope.Data, null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Khong ket noi duoc tournament-service (DELETE {Path})", path);
                return (default, "Khong ket noi duoc may chu giai dau!");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout goi tournament-service (DELETE {Path})", path);
                return (default, "May chu giai dau phan hoi qua cham!");
            }
        }

        private async Task<ApiResponseDto<T>?> ReadEnvelope<T>(HttpResponseMessage response)
        {
            // Java tra ve ApiResponse{success,message,data} ke ca khi loi (400/403/404/500)
            // — van doc duoc body de lay message, khong dua vao IsSuccessStatusCode.
            try
            {
                return await response.Content.ReadFromJsonAsync<ApiResponseDto<T>>(ReadOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string BuildQuery(Dictionary<string, string?> parameters)
        {
            var parts = parameters
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
            var joined = string.Join("&", parts);
            return joined.Length == 0 ? "" : "?" + joined;
        }

        // ════════════════════════════════════════════════════════════
        // Mapper: DTO JSON (Java) → EFCore model hien co (Views dung)
        // ════════════════════════════════════════════════════════════

        private static GiaiDau ToEfGiaiDau(GiaiDauApiDto d)
        {
            return new GiaiDau
            {
                Id = d.Id,
                TenGiai = d.TenGiai ?? "",
                MoTa = d.MoTa,
                SanBongId = d.SanBongId,
                OwnerId = d.OwnerId,
                SoDoiToiDa = d.SoDoiToiDa,
                SoBang = d.SoBang,
                LePhiGiai = d.LePhiGiai,
                TienKyQuy = d.TienKyQuy,
                TienPhatTheVang = d.TienPhatTheVang,
                TienPhatTheDo = d.TienPhatTheDo,
                SoTranTreoGioTheDo = d.SoTranTreoGioTheDo,
                SoTheVangTichLuy = d.SoTheVangTichLuy,
                NgayBatDau = d.NgayBatDau,
                NgayKetThuc = d.NgayKetThuc,
                ThoiGianTao = d.ThoiGianTao,
                ThoiGianDongDanhSach = d.ThoiGianDongDanhSach,
                TrangThai = d.TrangThai ?? "Draft",
                SanBong = new SanBong { Id = d.SanBongId, TenSan = d.TenSan ?? "", Quan = d.Quan ?? "" },
                Owner = new User { Id = d.OwnerId, HoTen = d.OwnerHoTen ?? "", Email = d.OwnerEmail ?? "" },
                BangDaus = (d.BangDaus ?? new()).Select(b => new BangDau { Id = b.Id, TenBang = b.TenBang ?? "" }).ToList(),
                DoiBongs = (d.DoiBongs ?? new()).Select(ToEfDoiBong).ToList(),
                TranDaus = (d.TranDaus ?? new()).Select(ToEfTranDau).ToList()
            };
        }

        private static DoiBong ToEfDoiBong(DoiBongApiDto d)
        {
            var doi = new DoiBong
            {
                Id = d.Id,
                GiaiDauId = d.GiaiDauId,
                BangId = d.BangId,
                DoiTruongId = d.DoiTruongId,
                TenDoi = d.TenDoi ?? "",
                LogoUrl = d.LogoUrl,
                TienKyQuyConLai = d.TienKyQuyConLai,
                DaThanhToan = d.DaThanhToan,
                ThoiGianThanhToan = d.ThoiGianThanhToan,
                TrangThai = d.TrangThai ?? "Active",
                ThoiGianTao = d.ThoiGianTao,
                DoiTruong = new User { Id = d.DoiTruongId, HoTen = d.DoiTruongHoTen ?? "", Email = d.DoiTruongEmail ?? "" },
                ThanhViens = (d.ThanhViens ?? new()).Select(ToEfThanhVien).ToList()
            };
            if (d.BangId.HasValue)
                doi.Bang = new BangDau { Id = d.BangId.Value, TenBang = d.TenBang ?? "" };
            return doi;
        }

        private static ThanhVienDoi ToEfThanhVien(ThanhVienDoiApiDto d)
        {
            return new ThanhVienDoi
            {
                Id = d.Id,
                DoiId = d.DoiId,
                HoTen = d.HoTen ?? "",
                SoAo = d.SoAo,
                AnhDaiDien = d.AnhDaiDien,
                SoTranTreoGio = d.SoTranTreoGio,
                TongBanThang = d.TongBanThang,
                TongTheVang = d.TongTheVang,
                TongTheDo = d.TongTheDo
            };
        }

        private static TranDau ToEfTranDau(TranDauApiDto d)
        {
            var tran = new TranDau
            {
                Id = d.Id,
                GiaiDauId = d.GiaiDauId,
                BangId = d.BangId,
                KhungGioId = null,
                DoiNhaId = d.DoiNhaId,
                DoiKhachId = d.DoiKhachId,
                BanThangNha = d.BanThangNha,
                BanThangKhach = d.BanThangKhach,
                VongDau = d.VongDau,
                LoaiVong = d.LoaiVong ?? "VongBang",
                NgayThiDau = d.NgayThiDau,
                TrangThai = d.TrangThai ?? "Scheduled",
                StaffPhuTrachId = d.StaffPhuTrachId,
                GiaiDau = new GiaiDau
                {
                    Id = d.GiaiDauId,
                    TenGiai = d.GiaiTenGiai ?? "",
                    TienPhatTheVang = d.GiaiTienPhatTheVang ?? 0,
                    TienPhatTheDo = d.GiaiTienPhatTheDo ?? 0,
                    SanBong = new SanBong { TenSan = d.SanBongTenSan ?? "" }
                }
            };

            if (d.BangId.HasValue)
                tran.BangDau = new BangDau { Id = d.BangId.Value, TenBang = d.TenBang ?? "" };

            if (d.DoiNhaId.HasValue)
            {
                tran.DoiNha = new DoiBong { Id = d.DoiNhaId.Value, TenDoi = d.TenDoiNha ?? "" };
                tran.DoiNha.ThanhViens = (d.DoiNhaThanhViens ?? new()).Select(ToEfThanhVien).ToList();
            }
            if (d.DoiKhachId.HasValue)
            {
                tran.DoiKhach = new DoiBong { Id = d.DoiKhachId.Value, TenDoi = d.TenDoiKhach ?? "" };
                tran.DoiKhach.ThanhViens = (d.DoiKhachThanhViens ?? new()).Select(ToEfThanhVien).ToList();
            }
            if (d.StaffPhuTrachId.HasValue)
                tran.StaffPhuTrach = new User { Id = d.StaffPhuTrachId.Value, HoTen = d.StaffPhuTrachHoTen ?? "" };

            return tran;
        }

        private static StandingRow ToStandingRow(StandingRowApiDto d)
        {
            return new StandingRow
            {
                DoiId = d.DoiId,
                TenDoi = d.TenDoi ?? "",
                LogoUrl = d.LogoUrl,
                ThuHang = d.ThuHang,
                SoTran = d.SoTran,
                Thang = d.Thang,
                Hoa = d.Hoa,
                Thua = d.Thua,
                BanThang = d.BanThang,
                BanThua = d.BanThua,
                Diem = d.Diem
            };
        }

        // ════════════════════════════════════════════════════════════
        // DTO doc JSON tra ve tu Java (ten field khop camelCase qua
        // PropertyNameCaseInsensitive — khong can [JsonPropertyName]).
        // ════════════════════════════════════════════════════════════

        private class ApiResponseDto<T>
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public T? Data { get; set; }
        }

        private class BangDauSummaryApiDto
        {
            public int Id { get; set; }
            public string? TenBang { get; set; }
        }

        private class GiaiDauApiDto
        {
            public int Id { get; set; }
            public string? TenGiai { get; set; }
            public string? MoTa { get; set; }
            public int SanBongId { get; set; }
            public string? TenSan { get; set; }
            public string? Quan { get; set; }
            public int OwnerId { get; set; }
            public string? OwnerHoTen { get; set; }
            public string? OwnerEmail { get; set; }
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
            public string? TrangThai { get; set; }
            public long SoDoiDaDangKy { get; set; }
            public long SoDoiDaThanhToan { get; set; }
            public List<BangDauSummaryApiDto>? BangDaus { get; set; }
            public List<DoiBongApiDto>? DoiBongs { get; set; }
            public List<TranDauApiDto>? TranDaus { get; set; }
        }

        private class DoiBongApiDto
        {
            public int Id { get; set; }
            public int GiaiDauId { get; set; }
            public int? BangId { get; set; }
            public string? TenBang { get; set; }
            public int DoiTruongId { get; set; }
            public string? DoiTruongHoTen { get; set; }
            public string? DoiTruongEmail { get; set; }
            public string? TenDoi { get; set; }
            public string? LogoUrl { get; set; }
            public decimal TienKyQuyConLai { get; set; }
            public bool DaThanhToan { get; set; }
            public DateTime? ThoiGianThanhToan { get; set; }
            public string? TrangThai { get; set; }
            public DateTime ThoiGianTao { get; set; }
            public List<ThanhVienDoiApiDto>? ThanhViens { get; set; }
        }

        private class ThanhVienDoiApiDto
        {
            public int Id { get; set; }
            public int DoiId { get; set; }
            public string? HoTen { get; set; }
            public int SoAo { get; set; }
            public string? AnhDaiDien { get; set; }
            public int SoTranTreoGio { get; set; }
            public int TongBanThang { get; set; }
            public int TongTheVang { get; set; }
            public int TongTheDo { get; set; }
        }

        private class TranDauApiDto
        {
            public int Id { get; set; }
            public int GiaiDauId { get; set; }
            public int? BangId { get; set; }
            public string? TenBang { get; set; }
            public int? DoiNhaId { get; set; }
            public string? TenDoiNha { get; set; }
            public int? DoiKhachId { get; set; }
            public string? TenDoiKhach { get; set; }
            public int? BanThangNha { get; set; }
            public int? BanThangKhach { get; set; }
            public int VongDau { get; set; }
            public string? LoaiVong { get; set; }
            public DateTime NgayThiDau { get; set; }
            public string? TrangThai { get; set; }
            public int? StaffPhuTrachId { get; set; }
            public List<ThanhVienDoiApiDto>? DoiNhaThanhViens { get; set; }
            public List<ThanhVienDoiApiDto>? DoiKhachThanhViens { get; set; }
            public string? StaffPhuTrachHoTen { get; set; }
            public string? GiaiTenGiai { get; set; }
            public decimal? GiaiTienPhatTheVang { get; set; }
            public decimal? GiaiTienPhatTheDo { get; set; }
            public string? SanBongTenSan { get; set; }
        }

        private class StandingRowApiDto
        {
            public int DoiId { get; set; }
            public string? TenDoi { get; set; }
            public string? LogoUrl { get; set; }
            public int ThuHang { get; set; }
            public int SoTran { get; set; }
            public int Thang { get; set; }
            public int Hoa { get; set; }
            public int Thua { get; set; }
            public int BanThang { get; set; }
            public int BanThua { get; set; }
            public int Diem { get; set; }
        }

        private class EventResultApiDto
        {
            public string? LoaiThucTe { get; set; }
            public long TysoNha { get; set; }
            public long TysoKhach { get; set; }
        }

        private class ConfirmResultApiDto
        {
            public TranDauApiDto? TranDau { get; set; }
        }

        public class KpiApiDto
        {
            public long TongGiai { get; set; }
            public long ChoDuyet { get; set; }
            public long DaDuyet { get; set; }
            public long DangDienRa { get; set; }
            public long DangDangKy { get; set; }
            public decimal TongLePhi { get; set; }
        }

        public class BaoCaoApiDto
        {
            public List<ThangKeApiDto> Bieu6Thang { get; set; } = new();
            public List<TopOwnerRowApiDto> TopOwner { get; set; } = new();
            public long TongGiaiDau { get; set; }
            public long GiaiHoanThanh { get; set; }
            public long TongDoi { get; set; }
            public long TongTranDau { get; set; }
            public decimal TongDoanhThu { get; set; }
        }

        public class ThangKeApiDto
        {
            public string Thang { get; set; } = "";
            public long SoGiai { get; set; }
            public long SoDoiThanhToan { get; set; }
            public decimal DoanhThu { get; set; }
        }

        public class TopOwnerRowApiDto
        {
            public string HoTen { get; set; } = "";
            public string Email { get; set; } = "";
            public long SoGiai { get; set; }
            public long SoActive { get; set; }
        }
    }
}
