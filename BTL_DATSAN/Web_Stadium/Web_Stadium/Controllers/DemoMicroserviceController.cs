using Microsoft.AspNetCore.Mvc;
using Web_Stadium.Services;

namespace Web_Stadium.Controllers
{
    [ApiController]
    [Route("api/demo/microservices")]
    public class DemoMicroserviceController : ControllerBase
    {
        private readonly GoStaffApiClient _goStaffApiClient;
        private readonly IConfiguration _configuration;

        public DemoMicroserviceController(GoStaffApiClient goStaffApiClient, IConfiguration configuration)
        {
            _goStaffApiClient = goStaffApiClient;
            _configuration = configuration;
        }

        [HttpGet]
        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            var goStatus = await _goStaffApiClient.GetDemoStatusAsync();
            var mvcUrl = $"{Request.Scheme}://{Request.Host}";
            var goApiUrl = _configuration["GoStaffApi:BaseUrl"] ?? "http://localhost:8081";

            return Ok(new
            {
                ok = goStatus.Ok && goStatus.DbStatus == "connected",
                generatedAt = DateTime.Now,
                architecture = "Browser -> ASP.NET Core MVC -> Go Staff API -> SQL Server",
                services = new[]
                {
                    new
                    {
                        name = "PitchHub MVC",
                        technology = ".NET 8 ASP.NET Core MVC",
                        url = mvcUrl,
                        role = "Main gateway: UI, login, routing, orchestration",
                        status = "running"
                    },
                    new
                    {
                        name = "Go Staff API",
                        technology = "Go microservice",
                        url = goApiUrl,
                        role = "Staff check-in business logic, permission validation, audit log",
                        status = goStatus.Ok ? "connected" : "disconnected"
                    },
                    new
                    {
                        name = "SQL Server",
                        technology = "SQL Server Express",
                        url = "localhost\\SQLEXPRESS/SanBongBTL",
                        role = "Shared data store for MVC and Go service",
                        status = goStatus.DbStatus
                    }
                },
                backendFlows = new[]
                {
                    new
                    {
                        name = "Staff booking check-in",
                        mvcScreen = "/Staff/CheckIn",
                        mvcAction = "StaffController.ThucHienCheckIn",
                        goEndpoint = "POST /api/staff/bookings/{bookingId}/check-in",
                        databaseEffects = new[]
                        {
                            "DatSans.TrangThai: DaXacNhan -> DangSuDung",
                            "DatSans.StaffCheckInId updated",
                            "DichVus stock deducted for prebooked services",
                            "AuditLogs row inserted"
                        }
                    },
                    new
                    {
                        name = "Tournament player check-in backend endpoint",
                        mvcScreen = "Postman/backend demo",
                        mvcAction = "GoStaffApiClient.GetCheckInsAsync / TogglePlayerCheckInAsync",
                        goEndpoint = "GET/POST /api/tournament/matches/{matchId}/checkins",
                        databaseEffects = new[]
                        {
                            "TournamentPlayerCheckIns row created or updated",
                            "Go API persists check-in state in SQL Server"
                        }
                    }
                },
                demoCommands = new[]
                {
                    "curl http://localhost:5000/api/demo/microservices/status",
                    "curl http://localhost:8081/health",
                    "curl http://localhost:8081/api/demo/status",
                    "curl -X POST http://localhost:8081/api/staff/bookings/1/check-in -H \"X-Internal-Api-Key: dev-demo-key\" -H \"X-Staff-Id: 5\""
                },
                goStatus
            });
        }
    }
}
