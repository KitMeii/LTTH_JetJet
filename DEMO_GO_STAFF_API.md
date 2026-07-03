# Demo ASP.NET MVC + Go API cho Staff

## Pham vi

- Nhom minh phu trach backend tich hop he thong.
- ASP.NET Core MVC van la he thong chinh va giu giao dien Staff.
- Go API la microservice rieng, xu ly nghiep vu Staff check-in can demo.
- Khong sua rong Owner/Admin/User/Tournament public cua nhom khac.

## Staff da chuyen sang Go o dau?

1. Staff check-in don dat san thuong:
   - UI MVC: `/Staff`, `/Staff/CheckIn`
   - MVC action: `StaffController.ThucHienCheckIn`
   - MVC goi Go API: `POST /api/staff/bookings/{bookingId}/check-in`
   - Go validate staff co duoc phan cong san khong.
   - Go doi `DatSans.TrangThai` tu `DaXacNhan` sang `DangSuDung`.
   - Go set `StaffCheckInId`.
   - Go tru ton kho dich vu dat truoc trong `DichVus`.
   - Go ghi `AuditLogs`.

2. Go API check-in cau thu giai dau:
   - Co endpoint Go de demo bang Postman/backend.
   - `GET /api/tournament/matches/{matchId}/checkins`
   - `POST /api/tournament/matches/{matchId}/checkins/{playerId}/toggle`
   - Go luu bang `TournamentPlayerCheckIns`.
   - Tren branch GitHub nay, MVC TournamentStaff van giu service giai dau rieng de khong dung module cua nhom khac.

## Cach chay

Terminal 1 - Go API:

```powershell
cd C:\Users\Admin\Downloads\LTTH_JetJet-main\LTTH_JetJet-main\GoStaffApi
$env:GO_STAFF_DB="server=localhost\\SQLEXPRESS;database=SanBongBTL;trusted_connection=true;TrustServerCertificate=true"
$env:GO_STAFF_API_KEY="dev-demo-key"
go run ./cmd/server
```

Terminal 2 - ASP.NET MVC:

```powershell
cd C:\Users\Admin\Downloads\LTTH_JetJet-main\LTTH_JetJet-main\BTL_DATSAN\Web_Stadium\Web_Stadium
dotnet run --urls http://localhost:5000
```

## URL demo

- MVC: `http://localhost:5000`
- Backend architecture status: `http://localhost:5000/api/demo/microservices/status`
- Go health: `http://localhost:8081/health`
- Go status: `http://localhost:8081/api/demo/status`
- Staff dashboard: `http://localhost:5000/Staff`
- Staff check-in don san: `http://localhost:5000/Staff/CheckIn`
- Staff giai dau: `http://localhost:5000/TournamentStaff/DanhSach`

## Cach demo de giong backend that

Khong dua trang "microservice" len navbar. Nguoi dung binh thuong chi thay web PitchHub.

Khi bao cao, mo 3 man hinh:

1. Web MVC `http://localhost:5000/Staff/CheckIn`
2. Postman hoac terminal goi backend status:

```powershell
curl http://localhost:5000/api/demo/microservices/status
```

3. Terminal Go API va SSMS de thay log + database doi that.

Y tuong noi:

- "Frontend khong biet chi tiet service con. Web MVC chi goi backend qua client."
- "Endpoint `/api/demo/microservices/status` la endpoint noi bo de kiem tra kien truc khi demo."
- "Go API chay cong rieng 8081, MVC chay cong 5000, SQL Server la data store chung."
- "Khi check-in, MVC goi Go API, Go validate quyen staff, update SQL, ghi audit log."

## Tai khoan

| Vai tro | Email | Mat khau | Ghi chu |
| --- | --- | --- | --- |
| Admin | `admin@pitchhub.vn` | `admin123` | Quan tri |
| Owner | `owner1@gmail.com` | `owner123` | Chu san |
| Owner | `owner2@gmail.com` | `owner123` | Chu san |
| Staff | `staff@pitchhub.vn` | `Staff@123` | Staff san 1, 2 |
| Staff | `staff1@pitchhub.vn` | `staff123` | Nen dung de demo |
| Staff | `staff2@pitchhub.vn` | `staff123` | Staff san 6, 7 |
| User | `user1@gmail.com` | `user123` | Khach hang |
| User | `user2@gmail.com` | `user123` | Khach hang |

## Phan mem ben thu 3 nen dung de demo cho dep

1. SSMS:
   - Mo database `SanBongBTL`.
   - Xem bang `DatSans`, `TournamentPlayerCheckIns`, `AuditLogs`.
   - Sau khi bam check-in, refresh bang de thay du lieu doi that.

2. Postman hoac Insomnia:
   - Goi truc tiep Go API de chung minh day la service rieng.
   - Nen dung Postman vi thay thay request, header, response JSON ro hon.
   - Import collection: [`GoStaffApi.postman_collection.json`](GoStaffApi/GoStaffApi.postman_collection.json).
   - Header:
     - `X-Internal-Api-Key: dev-demo-key`
     - `X-Staff-Id: <staff id>`

3. Terminal Go:
   - De canh man hinh log.
   - Khi Staff bam check-in, terminal hien request `staffId`, `bookingId` hoac `matchId/playerId`.

## Demo bang Postman

Import file:

```text
GoStaffApi/GoStaffApi.postman_collection.json
```

Bien Postman can de:

| Bien | Gia tri demo | Ghi chu |
| --- | --- | --- |
| `baseUrl` | `http://localhost:8081` | Go API |
| `apiKey` | `dev-demo-key` | Noi bo giua MVC va Go |
| `staffId` | `7` | `staff1@pitchhub.vn` |
| `bookingId` | `14` hoac `9` hoac `1` | Booking `DaXacNhan`, chua check-in |
| `matchId` | tuy DB giai dau | Dung cho check-in cau thu |
| `playerId` | tuy DB giai dau | Dung cho toggle cau thu |

Thu tu bam trong Postman:

1. `Health - public`: chung minh Go service dang song.
2. `Demo status - public`: chung minh Go noi SQL Server.
3. `Staff booking check-in - Go backend`: chung minh Go xu ly Staff check-in.
4. Mo SSMS, refresh `DatSans` va `AuditLogs`.
5. Nhin terminal Go: co log `staffId`, `bookingId`, `action=booking_checked_in`.

## Cau lenh Postman/curl mau

Health:

```powershell
curl http://localhost:8081/health
```

Status:

```powershell
curl http://localhost:8081/api/demo/status
```

Check-in don dat san bang Go:

```powershell
curl -X POST http://localhost:8081/api/staff/bookings/1/check-in `
  -H "X-Internal-Api-Key: dev-demo-key" `
  -H "X-Staff-Id: 5"
```

Lay danh sach booking co the demo trong SSMS:

```sql
SELECT TOP 20
    d.Id,
    d.MaXacNhan,
    d.TrangThai,
    d.NgayThiDau,
    u.Email,
    u.HoTen,
    s.TenSan,
    k.SanBongId
FROM DatSans d
JOIN Users u ON u.Id = d.UserId
JOIN KhungGios k ON k.Id = d.KhungGioId
JOIN SanBongs s ON s.Id = k.SanBongId
WHERE d.TrangThai = 'DaXacNhan'
ORDER BY d.NgayThiDau DESC;
```

## Ban do code Staff

Click vao ten file/function de nhay thang sang code.

### ASP.NET MVC Staff

| Code | Chuc nang | Demo lien quan |
| --- | --- | --- |
| [`StaffController`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L10) | Controller chinh cua staff | Moi flow Staff vao day |
| [`GoStaffApiClient inject`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L15) | Gan Go client vao StaffController | Chung minh MVC co goi Go |
| [`GetSanDuocGiaoAsync`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L28) | Lay danh sach san staff duoc phan cong | Quyen staff theo san |
| [`Index`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L40) | Dashboard ca truc, don hom nay | Man `/Staff` |
| [`CheckIn`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L84) | Tim booking theo ma/SĐT | Man `/Staff/CheckIn` |
| [`ThucHienCheckIn`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L113) | MVC goi Go API check-in booking | Diem demo chinh |
| [`POS`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L131) | Them dich vu cho don dang su dung | Van giu C# |
| [`ThemDichVuPOS`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L154) | Luu dich vu POS, tru kho | Van giu C# |
| [`CheckOut`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L205) | Man thu tien checkout | Van giu C# |
| [`ThucHienCheckOut`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L230) | Hoan thanh don, ghi checkout | Van giu C# |
| [`GhiNhanSuCo`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L308) | Ghi su co cua don san | Van giu C# |
| [`VangLai`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L336) | Ban dich vu vang lai | Van giu C# |
| [`HoSo`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L427) | Ho so, lich su ca truc | Van giu C# |
| [`YeuCauDoiGio`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L475) | Xu ly yeu cau doi gio | Van giu C# |
| [`ChuyenNhuong`](BTL_DATSAN/Web_Stadium/Web_Stadium/Controllers/StaffController.cs#L558) | Xu ly chuyen nhuong slot | Van giu C# |

### ASP.NET MVC goi Go

| Code | Chuc nang | Demo lien quan |
| --- | --- | --- |
| [`GoStaffApiClient`](BTL_DATSAN/Web_Stadium/Web_Stadium/Services/GoStaffApiClient.cs#L6) | Client HTTP noi MVC voi Go | Cau noi tich hop |
| [`GetCheckInsAsync`](BTL_DATSAN/Web_Stadium/Web_Stadium/Services/GoStaffApiClient.cs#L25) | Lay danh sach cau thu da check-in | Giai dau |
| [`TogglePlayerCheckInAsync`](BTL_DATSAN/Web_Stadium/Web_Stadium/Services/GoStaffApiClient.cs#L51) | Tick/bo tick cau thu | Giai dau |
| [`CheckInBookingAsync`](BTL_DATSAN/Web_Stadium/Web_Stadium/Services/GoStaffApiClient.cs#L75) | Goi Go check-in don san | Demo chinh |
| [`GetDemoStatusAsync`](BTL_DATSAN/Web_Stadium/Web_Stadium/Services/GoStaffApiClient.cs#L99) | Goi status Go API | Nut/endpoint demo |
| [`CreateRequest`](BTL_DATSAN/Web_Stadium/Web_Stadium/Services/GoStaffApiClient.cs#L124) | Gan `X-Internal-Api-Key`, `X-Staff-Id` | Bao ve API noi bo |

### Go API

| Code | Chuc nang | Demo lien quan |
| --- | --- | --- |
| [`Register routes`](GoStaffApi/internal/transport/http/handler.go#L25) | Khai bao endpoint Go | Cho thay route API |
| [`requireAPIKey`](GoStaffApi/internal/transport/http/handler.go#L39) | Kiem tra `X-Internal-Api-Key` | Bao ve service noi bo |
| [`getCheckIns`](GoStaffApi/internal/transport/http/handler.go#L52) | API lay check-in cau thu | Giai dau |
| [`togglePlayer`](GoStaffApi/internal/transport/http/handler.go#L74) | API tick/bo tick cau thu | Giai dau |
| [`checkInBooking`](GoStaffApi/internal/transport/http/handler.go#L108) | API check-in booking Staff | Demo chinh |
| [`demoStatus`](GoStaffApi/internal/transport/http/handler.go#L134) | API status/uptime/db | Postman demo |
| [`EnsureSchema`](GoStaffApi/internal/storage/sqlserver/store.go#L22) | Tao bang `TournamentPlayerCheckIns` neu thieu | Setup DB |
| [`GetCheckIns`](GoStaffApi/internal/storage/sqlserver/store.go#L50) | Doc check-in cau thu tu SQL | Reload van con |
| [`TogglePlayer`](GoStaffApi/internal/storage/sqlserver/store.go#L87) | Luu toggle cau thu vao SQL | Giai dau |
| [`CheckInBooking`](GoStaffApi/internal/storage/sqlserver/store.go#L168) | Validate staff, update booking, tru kho, ghi audit | Demo chinh |
| [`DemoStatus`](GoStaffApi/internal/storage/sqlserver/store.go#L259) | Ping DB, dem record | Postman status |
| [`validateMatchAccess`](GoStaffApi/internal/storage/sqlserver/store.go#L280) | Validate staff + tran + cau thu | Giai dau |

## Kich ban noi voi thay

1. "Day la ung dung chinh ASP.NET MVC, Staff dang thao tac tren giao dien cu."
2. "Nhung nut check-in khong xu ly truc tiep trong MVC nua."
3. "MVC goi Go microservice qua HTTP, co header API key va staff id."
4. "Go API validate quyen staff theo bang `StaffSanPhanCong`."
5. "Go API cap nhat SQL Server: doi trang thai booking, tru ton kho dich vu, ghi audit log."
6. "Terminal Go in log request nen thay thay day la service rieng dang nhan request."
7. "Neu tat Go API, MVC khong crash ma bao loi Go API chua chay."
8. "Voi giai dau, co the demo endpoint Go bang Postman; MVC TournamentStaff van giu service giai dau cua nhom khac."

## Lenh kiem thu

```powershell
cd C:\Users\Admin\Downloads\LTTH_JetJet-main\LTTH_JetJet-main\GoStaffApi
go test ./...
go build ./cmd/server

cd C:\Users\Admin\Downloads\LTTH_JetJet-main\LTTH_JetJet-main\BTL_DATSAN\Web_Stadium\Web_Stadium
dotnet build
```
