# Go Staff Check-in API

Microservice **điểm danh cầu thủ giải đấu** cho PitchHub — tách biệt hoàn
toàn với chức năng check-in đơn đặt sân thường (đã có sẵn ở C#
`StaffController.ThucHienCheckIn()` và Java `java-recommendation`
`CheckInService.java` — không lặp lại ở đây nữa).

Cổng: `8080` = tournament-service (Java), `8081` = java-recommendation
(Java), **`8082` = GoStaffApi**.

## Run

Xem `.env.example` để biết đủ 3 biến môi trường cần set (`GO_STAFF_PORT`,
`GO_STAFF_DB`, `GO_JWT_SECRET`). Go không tự đọc file `.env` — phải export
thật qua PowerShell:

```powershell
cd GoStaffApi
$env:GO_STAFF_PORT="8082"
$env:GO_STAFF_DB="server=localhost;database=SanBongBTL;user id=sa;password=422005;TrustServerCertificate=true"
$env:GO_JWT_SECRET="SanBongBTL_SecretKey_2024_XYZ_MustBe32Chars"
go run ./cmd/server
```

## Endpoints

Công khai:

```text
GET /health
GET /api/demo/status
```

Điểm danh cầu thủ giải đấu — yêu cầu JWT hợp lệ, role `Staff` (đọc cookie
`jwt` trước, fallback header `Authorization: Bearer <token>` — cùng cơ chế
với tournament-service/java-recommendation, **không còn dùng**
`X-Internal-Api-Key`/`X-Staff-Id` nữa):

```text
GET  /api/tournament/matches/{matchId}/checkins
POST /api/tournament/matches/{matchId}/checkins/{playerId}/toggle
```

## Test

```powershell
go build ./cmd/server
```
