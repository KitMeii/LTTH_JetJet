# Go Staff Check-in API

Microservice demo for Staff backend integration.

## Run

```powershell
cd C:\Users\Admin\Downloads\LTTH_JetJet-main\LTTH_JetJet-main\GoStaffApi
$env:GO_STAFF_DB="server=localhost\\SQLEXPRESS;database=SanBongBTL;trusted_connection=true;TrustServerCertificate=true"
$env:GO_STAFF_API_KEY="dev-demo-key"
go run ./cmd/server
```

## Endpoints

Public:

```text
GET /health
GET /api/demo/status
```

Protected headers:

```text
X-Internal-Api-Key: dev-demo-key
X-Staff-Id: <staff id>
```

Staff booking check-in:

```text
POST /api/staff/bookings/{bookingId}/check-in
```

Tournament player check-in:

```text
GET  /api/tournament/matches/{matchId}/checkins
POST /api/tournament/matches/{matchId}/checkins/{playerId}/toggle
```

## Test

```powershell
go test ./...
go build ./cmd/server
```
