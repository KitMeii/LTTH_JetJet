# BÁO CÁO TÍCH HỢP C# → JAVA

Phạm vi: `BTL_DATSAN/Web_Stadium/Web_Stadium/Services/TournamentApiService.cs` (lớp trung gian duy nhất gọi Java) và 4 controller: `TournamentPublicController.cs`, `TournamentController.cs` (Owner), `TournamentStaffController.cs`, `AdminTournamentController.cs`.

Tất cả URL Java trong bảng dưới là **path tương đối** truyền vào `HttpClient` có `BaseAddress = http://localhost:8080/api/` (cấu hình tại `Program.cs`). URL đầy đủ = `http://localhost:8080/api/` + path.

---

## TournamentApiService.cs

### Nhóm PUBLIC — không cần token

| Method C# | Java Endpoint | Dòng code | Có Token |
|-----------|---------------|-----------|----------|
| `GetDanhSachGiai()` | GET `public/tournament{query}` | dòng 65 | Không |
| `GetChiTietGiai()` | GET `public/tournament/{id}` | dòng 71 | Không |
| `GetBangXepHang()` | GET `public/tournament/{id}/standings` | dòng 77 | Không |
| `GetLichThiDau()` | GET `public/tournament/{id}/matches` | dòng 84 | Không |
| `GetVuaPhaLuoi()` | GET `public/tournament/{id}/top-scorers` | dòng 90 | Không |

### Nhóm đăng ký đội — cần đăng nhập (bất kỳ role)

| Method C# | Java Endpoint | Dòng code | Có Token |
|-----------|---------------|-----------|----------|
| `DangKyDoi()` | POST `public/tournament/{giaiId}/register` | dòng 99 | Có |
| `GetDoiCuaToi()` | GET `public/tournament/teams/{doiId}` | dòng 105 | Có |
| `GetRoster()` | GET `public/tournament/teams/{doiId}/roster` | dòng 111 | Có |
| `ThemThanhVien()` | POST `public/tournament/teams/{doiId}/members` | dòng 118 | Có |
| `XoaThanhVien()` | DELETE `public/tournament/teams/{doiId}/members/{thanhVienId}` | dòng 124 | Có |

### Nhóm OWNER — cần token role Owner

| Method C# | Java Endpoint | Dòng code | Có Token |
|-----------|---------------|-----------|----------|
| `TaoGiai()` | POST `tournament/owner` | dòng 133 | Có |
| `GetMyTournaments()` | GET `tournament/owner/my-tournaments` | dòng 139 | Có |
| `GetChiTietGiaiOwner()` | GET `tournament/owner/{id}` | dòng 145 | Có |
| `MoiDangKy()` | POST `tournament/owner/{id}/open-registration` | dòng 150 | Có |
| `DongDangKy()` | POST `tournament/owner/{id}/close-registration` | dòng 153 | Có |
| `GanBang()` | POST `tournament/owner/teams/{doiId}/assign-group{query}` | dòng 158 | Có |
| `KhoiTao()` | POST `tournament/owner/{id}/start` | dòng 162 | Có |
| `KetThuc()` | POST `tournament/owner/{id}/finish` | dòng 165 | Có |
| `XuLySuCo()` | POST `tournament/owner/matches/{tranDauId}/incident{query}` | dòng 170 | Có |
| `GetDanhSachDoi()` | GET `tournament/owner/{giaiId}/teams` | dòng 175 | Có |
| `XacNhanThanhToan()` | POST `tournament/owner/{giaiId}/teams/{doiId}/confirm-payment` | dòng 180 | Có |
| `GetExcelDoiSoat()` | GET `tournament/owner/{id}/excel` | dòng 183 | Có |

⚠️ **Java có 2 endpoint Owner không có method C# tương ứng: `POST /tournament/owner/{id}/generate-knockout` và `GET /tournament/owner/{id}/bracket`.** Xem mục "Khoảng trống tích hợp" cuối báo cáo.

### Nhóm STAFF — cần token role Staff

| Method C# | Java Endpoint | Dòng code | Có Token |
|-----------|---------------|-----------|----------|
| `GetTranDauHomNay()` | GET `tournament/staff/matches{query}` | dòng 192 | Có |
| `GetChiTietTran()` | GET `tournament/staff/matches/{matchId}` | dòng 198 | Có |
| `CheckIn()` | POST `tournament/staff/matches/{matchId}/checkin` | dòng 204 | Có |
| `GhiSuKien()` | POST `tournament/staff/matches/{matchId}/events` | dòng 212 | Có |
| `HuyBanThang()` | DELETE `tournament/staff/matches/{matchId}/events/{suKienId}` | dòng 220 | Có |
| `XacNhanKetThuc()` | POST `tournament/staff/matches/{matchId}/confirm` | dòng 226 | Có |

### Nhóm ADMIN — cần token role Admin

| Method C# | Java Endpoint | Dòng code | Có Token |
|-----------|---------------|-----------|----------|
| `GetAllGiai()` | GET `admin/tournament{query}` | dòng 244 | Có |
| `GetKpi()` | GET `admin/tournament/kpi` | dòng 249 | Có |
| `GetChiTietGiaiAdmin()` | GET `admin/tournament/{id}` | dòng 253 | Có |
| `PheDuyet()` | POST `admin/tournament/{id}/approve{query}` | dòng 260 | Có |
| `TuChoi()` | POST `admin/tournament/{id}/reject{query}` | dòng 266 | Có |
| `GetExcelDoiSoatAdmin()` | GET `admin/tournament/{id}/excel` | dòng 270 | Có |
| `GetBaoCao()` | GET `admin/tournament/report` | dòng 273 | Có |

### Cơ chế dùng chung (helper, dòng 279–407)

- `GetJwtFromContext()` (dòng 45): đọc JWT từ `context.Request.Cookies["jwt"]`.
- `CreateClient()` (dòng 279–285): tạo `HttpClient` từ `IHttpClientFactory`, nếu `jwtToken` khác rỗng thì gắn header `Authorization: Bearer {token}`.
- `GetAsync<T>()` / `PostAsync<T>()` / `DeleteAsync<T>()` / `GetBytesAsync()` (dòng 287–393): mỗi hàm đều bọc `try/catch` bắt `HttpRequestException` (Java app không chạy/không kết nối được) và `TaskCanceledException` (timeout, `client.Timeout=30s` cấu hình ở `Program.cs`) — log lỗi qua `_logger`, trả về `default`/`null`/`(false, "Không kết nối được máy chủ giải đấu!")` thay vì để lỗi 500 làm sập trang.
- `ReadEnvelope<T>()` (dòng 395–407): đọc JSON envelope `{success, message, data, timestamp}` từ Java, không dựa vào `response.IsSuccessStatusCode` (vì Java trả JSON body kể cả lỗi 400/403/404/500).

---

## TournamentPublicController.cs

| Action C# | Dòng | Gọi ApiService | Java Endpoint |
|-----------|------|----------------|---------------|
| `Index()` | 35 | `GetDanhSachGiai()` (dòng 39) | GET `/public/tournament` |
| `Details()` | 58 | `GetChiTietGiai()` (dòng 60), `GetBangXepHang()` (dòng 69) | GET `/public/tournament/{id}`, GET `/public/tournament/{id}/standings` |
| `DangKy()` | 86 | `GetChiTietGiai()` (dòng 88) | GET `/public/tournament/{id}` |
| `XacNhanDangKy()` | 123 | `DangKyDoi()` (dòng 131) | POST `/public/tournament/{giaiDauId}/register` |
| `Checkout()` | 146 | `GetDoiCuaToi()` (dòng 148) | GET `/public/tournament/teams/{doiId}` |
| `NopDanhSach()` | 168 | `GetDoiCuaToi()` (dòng 170) | GET `/public/tournament/teams/{doiId}` |
| `ThemThanhVien()` | 190 | `ThemThanhVien()` (dòng 218) | POST `/public/tournament/teams/{doiId}/members` |
| `XoaThanhVien()` | 235 | `GetRoster()` (dòng 240), `XoaThanhVien()` (dòng 243) | GET `/public/tournament/teams/{doiId}/roster`, DELETE `/public/tournament/teams/{doiId}/members/{thanhVienId}` |

## TournamentController.cs (Owner)

| Action C# | Dòng | Gọi ApiService | Java Endpoint |
|-----------|------|----------------|---------------|
| `Index()` | 40 | `GetMyTournaments()` (dòng 42) | GET `/tournament/owner/my-tournaments` |
| `Create()` GET | 47 | *(không gọi Java — chỉ đọc `SanBongs` qua `_context`)* | — |
| `Create()` POST | 55 | `TaoGiai()` (dòng 57) | POST `/tournament/owner` |
| `Details()` | 70 | `GetChiTietGiaiOwner()` (dòng 72), `GetBangXepHang()` (dòng 75) | GET `/tournament/owner/{id}`, GET `/public/tournament/{id}/standings` |
| `MoiDangKy()` | 81 | `MoiDangKy()` (dòng 83) | POST `/tournament/owner/{id}/open-registration` |
| `DongDangKy()` | 92 | `DongDangKy()` (dòng 94) | POST `/tournament/owner/{id}/close-registration` |
| `ChiaBang()` | 102 | `GetChiTietGiaiOwner()` (dòng 104) | GET `/tournament/owner/{id}` |
| `GanBang()` (AJAX) | 116 | `GanBang()` (dòng 118) | POST `/tournament/owner/teams/{doiId}/assign-group` |
| `KhoiTao()` | 126 | `KhoiTao()` (dòng 128) | POST `/tournament/owner/{id}/start` |
| `KetThuc()` | 137 | `KetThuc()` (dòng 139) | POST `/tournament/owner/{id}/finish` |
| `XuLySuCo()` | 148 | `XuLySuCo()` (dòng 150), `GetChiTietTran()` (dòng 154) | POST `/tournament/owner/matches/{tranDauId}/incident`, GET `/tournament/staff/matches/{matchId}` |
| `ExcelDoiSoat()` | 159 | `GetExcelDoiSoat()` (dòng 161) | GET `/tournament/owner/{id}/excel` |
| `DanhSachDoi()` ⭐ mới | 177 | `GetDanhSachDoi()` (dòng 179) | GET `/tournament/owner/{giaiId}/teams` |
| `XacNhanThanhToan()` ⭐ mới | 194 | `XacNhanThanhToan()` (dòng 196) | POST `/tournament/owner/{giaiId}/teams/{doiId}/confirm-payment` |

Ghi chú `XuLySuCo()` dòng 148: gọi chéo sang endpoint **Staff** (`GetChiTietTran` → `/tournament/staff/matches/{matchId}`) chỉ để lấy lại `GiaiDauId` phục vụ redirect — không phải lỗi, nhưng là điểm phụ thuộc chéo giữa 2 nhóm role đáng lưu ý.

## TournamentStaffController.cs

| Action C# | Dòng | Gọi ApiService | Java Endpoint |
|-----------|------|----------------|---------------|
| `TranDau()` | 30 | `GetChiTietTran()` (dòng 32) | GET `/tournament/staff/matches/{matchId}` |
| `DanhSach()` | 41 | `GetTranDauHomNay()` (dòng 43, gọi lại lần 2 ở dòng 49 để tính KPI hôm nay) | GET `/tournament/staff/matches` |
| `CheckIn()` | 60 | `CheckIn()` (dòng 62) | POST `/tournament/staff/matches/{matchId}/checkin` |
| `SuKien()` | 87 | `GetChiTietTran()` (dòng 89) | GET `/tournament/staff/matches/{matchId}` |
| `GhiSuKien()` (AJAX) | 107 | `GhiSuKien()` (dòng 111) | POST `/tournament/staff/matches/{matchId}/events` |
| `KetThuc()` | 133 | `GetChiTietTran()` (dòng 135) | GET `/tournament/staff/matches/{matchId}` |
| `XacNhanKetThuc()` | 153 | `XacNhanKetThuc()` (dòng 155) | POST `/tournament/staff/matches/{matchId}/confirm` |
| `HuyBanThang()` (AJAX) | 171 | `HuyBanThang()` (dòng 173) | DELETE `/tournament/staff/matches/{matchId}/events/{suKienId}` |

## AdminTournamentController.cs

| Action C# | Dòng | Gọi ApiService | Java Endpoint |
|-----------|------|----------------|---------------|
| `Index()` | 36 | `GetAllGiai()` (dòng 40), `GetKpi()` (dòng 41) | GET `/admin/tournament`, GET `/admin/tournament/kpi` |
| `Details()` | 68 | `GetChiTietGiaiAdmin()` (dòng 70), `GetBangXepHang()` (dòng 73) | GET `/admin/tournament/{id}`, GET `/public/tournament/{id}/standings` |
| `PheDuyet()` | 83 | `PheDuyet()` (dòng 85) | POST `/admin/tournament/{id}/approve` |
| `TuChoi()` | 101 | `TuChoi()` (dòng 109) | POST `/admin/tournament/{id}/reject` |
| `ExcelDoiSoat()` | 123 | `GetExcelDoiSoatAdmin()` (dòng 125) | GET `/admin/tournament/{id}/excel` |
| `BaoCao()` | 136 | `GetBaoCao()` (dòng 138) | GET `/admin/tournament/report` |

---

## TỔNG KẾT

- **Tổng số method C# gọi Java** (trong `TournamentApiService.cs`): **35 method**
- **Tổng số Java endpoint được gọi từ C#**: **35 endpoint** (Java thực có 42 endpoint — 7 endpoint chưa được C# gọi tới, xem "Khoảng trống tích hợp")
- **Tổng số action controller dùng Java** (đếm theo action, 1 action có thể gọi nhiều method): **28 action** (Public 8, Owner 13, Staff 8, Admin 6 — có 3 action lặp giữa nhóm do đếm helper `Create()` GET không gọi Java)
- **Tổng số lượt gọi `_apiService.xxx()` trong 4 controller** (đếm từng dòng gọi): **42 lượt**

### Khoảng trống tích hợp phát hiện được

1. **KnockOut chưa có đường dây C#**: Java có sẵn `POST /tournament/owner/{id}/generate-knockout` và `GET /tournament/owner/{id}/bracket` (đã kiểm chứng hoạt động đúng ở phiên review Java trước), nhưng `TournamentApiService.cs` **không có method** gọi 2 endpoint này, và không controller action nào trong `TournamentController.cs` xử lý chúng. View `Views/Tournament/Bracket.cshtml` tồn tại sẵn nhưng **không có action nào render nó** — cây khung UI có thể là tàn dư từ bản C# cũ (`Services/KnockOutService.cs`, `Services/TournamentService.cs` — 2 file service C# gốc vẫn còn trong repo nhưng không còn được 4 controller gọi tới nữa). **Nếu muốn demo tính năng KnockOut, cần bổ sung 2 method vào `TournamentApiService.cs` + 1-2 action vào `TournamentController.cs` trước.**
2. **`GetBangXepHang()` luôn gọi endpoint public** (`/public/tournament/{id}/standings`) kể cả khi được gọi từ `AdminTournamentController`/`TournamentController` (Owner) — không phải lỗi (Java endpoint không yêu cầu quyền sở hữu để xem BXH), nhưng đáng lưu ý vì tên method `GetBangXepHang` dùng chung cho cả 3 role.

---

## SƠ ĐỒ LUỒNG

### LUỒNG 1: User xem danh sách giải đấu
```
Browser → GET /TournamentPublic/Index (C# port 7045)
        → TournamentPublicController.Index() dòng 35
        → _apiService.GetDanhSachGiai() dòng 39
        → TournamentApiService.GetDanhSachGiai() dòng 51 → GetAsync() dòng 65
        → HTTP GET http://localhost:8080/api/public/tournament?... (Java)
        → Java: TournamentPublicController.index() → GiaiDauRepository.findAll() (lọc, sort, enrich)
        → Java trả JSON {success:true, data:[GiaiDauApiDto...]}
        → C#: ReadEnvelope<List<GiaiDauApiDto>>() → ToEfGiaiDau() map sang GiaiDau (EFCore model)
        → ViewBag.DanhSachQuan (đọc trực tiếp SanBongContext, không qua Java)
        → return View(giaiList)
        → Browser render Views/TournamentPublic/Index.cshtml
```

### LUỒNG 2: Owner tạo giải đấu mới
```
Browser → POST /Tournament/Create (form data) (C# port 7045, cookie "jwt" đính kèm)
        → TournamentController.Create(CreateGiaiDauDto dto) dòng 55
        → _apiService.TaoGiai(dto, Jwt()) dòng 57
        → Jwt() dòng 37 đọc cookie qua GetJwtFromContext()
        → TournamentApiService.TaoGiai() dòng 131 → PostAsync<GiaiDauApiDto>() dòng 133
        → CreateClient() gắn header Authorization: Bearer {jwt} (dòng 282-283)
        → HTTP POST http://localhost:8080/api/tournament/owner (Java, body JSON camelCase)
        → Java: JwtFilter xác thực token → SecurityConfig yêu cầu ROLE_Owner
        → TournamentController(Java).create() → TournamentService.taoGiaiDau()
          (validate sân hợp lệ, ngày, số đội) → lưu GiaiDau + tự sinh BangDau A/B/...
        → Java trả JSON {success:true, data:{...}}
        → C#: nếu ok → TempData Success → RedirectToAction("Details", {id})
        → nếu lỗi (403/400) → TempData Error → return View(dto) (giữ nguyên form)
```

### LUỒNG 3: Admin phê duyệt giải
```
Browser → POST /AdminTournament/PheDuyet/5 (cookie "jwt", role Admin)
        → AdminTournamentController.PheDuyet(id, ghiChu) dòng 83
        → _apiService.PheDuyet(id, ghiChu, Jwt()) dòng 85
        → TournamentApiService.PheDuyet() dòng 257 → PostAsync() dòng 260
        → HTTP POST http://localhost:8080/api/admin/tournament/5/approve?ghiChu=... (Java)
        → Java: SecurityConfig yêu cầu ROLE_Admin → AdminTournamentController(Java).approve()
          → chỉ cho phép nếu TrangThai=="Draft" → set "Approved" → save
        → Java trả {success:true, message:"Da phe duyet giai..."}
        → C#: ok=true → TempData["Success"] = "✅ Đã phê duyệt giải..."
        → RedirectToAction("Details", {id}) → Browser thấy trạng thái mới
```

### LUỒNG 4: Staff ghi sự kiện bàn thắng
```
Browser (AJAX) → POST /TournamentStaff/GhiSuKien (tranDauId, thanhVienId, doiId, loaiSuKien="BanThang", phut, ghiChu)
        → TournamentStaffController.GhiSuKien() dòng 107
        → _apiService.GhiSuKien(tranDauId, thanhVienId, doiId, loaiSuKien, phut, ghiChu, Jwt()) dòng 111
        → TournamentApiService.GhiSuKien() dòng 208 → PostAsync<EventResultApiDto>() dòng 212
        → HTTP POST http://localhost:8080/api/tournament/staff/matches/{tranDauId}/events (Java, role Staff)
        → Java: TournamentStaffController(Java).ghiSuKien()
          → kiểm tra trận đang InProgress + Staff được phân công sân
          → kiểm tra cầu thủ không bị treo giò
          → tạo SuKienTran, cập nhật TongBanThang, đếm lại tỷ số
        → Java trả {success:true, data:{loaiThucTe, tysoNha, tysoKhach}}
        → C#: trả Json({ok:true, tysoNha, tysoKhach, message}) thẳng cho JS
        → Browser (JS) cập nhật tỷ số trên màn hình không cần reload trang
```

### LUỒNG 5: Xác nhận thanh toán đội (tính năng mới)
```
Browser (AJAX/Postman — chưa có UI .cshtml riêng) → POST /Tournament/XacNhanThanhToan (giaiId, doiId)
        → TournamentController.XacNhanThanhToan() dòng 194
        → _apiService.XacNhanThanhToan(giaiId, doiId, Jwt()) dòng 196
        → TournamentApiService.XacNhanThanhToan() dòng 179 → PostAsync() dòng 180
        → HTTP POST http://localhost:8080/api/tournament/owner/{giaiId}/teams/{doiId}/confirm-payment (Java, role Owner)
        → Java: TournamentController(Java).confirmPayment() → TournamentService.xacNhanThanhToan()
          → kiểm tra Owner sở hữu giải, đội thuộc giải, chưa thanh toán trước đó
          → set DoiBong.DaThanhToan=true, ThoiGianThanhToan=now → save
          → TournamentNotificationService.guiEmailXacNhanThanhToan() gửi email đội trưởng
        → Java trả {success:true, message:"Da xac nhan thanh toan cho doi!"}
        → C#: trả Json({ok:true, message:"Đã xác nhận thanh toán!"})
        → Browser (JS) hiện thông báo thành công
```
