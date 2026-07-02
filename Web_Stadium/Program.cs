using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Web_Stadium.EFCore;
using Web_Stadium.Hubs;

namespace Web_Stadium
{
    public class Program
    {

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                try
                {
                    System.IO.File.AppendAllText(
                        Path.Combine(Directory.GetCurrentDirectory(), "crash_log.txt"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AppDomain UnhandledException\n"
                        + (e.ExceptionObject?.ToString() ?? "unknown error") + "\n\n"
                    );
                }
                catch { /* ignore log failure */ }
            };
            // Bắt lỗi trên Task/async (quan trọng!) — gọi SetObserved để KHÔNG làm app crash
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                try
                {
                    System.IO.File.AppendAllText(
                        Path.Combine(Directory.GetCurrentDirectory(), "crash_log.txt"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UnobservedTaskException\n"
                        + (e.Exception?.ToString() ?? "unknown") + "\n\n"
                    );
                }
                catch { /* ignore log failure */ }
                e.SetObserved();
            };

            var builder = WebApplication.CreateBuilder(args);

            // Thêm middleware bắt lỗi toàn bộ request
            builder.Services.AddExceptionHandler(options =>
            {
                options.ExceptionHandlingPath = "/error";
            });

            

            // 1. Đăng ký DbContext - liên kết EFCore với SQL Server
            builder.Services.AddDbContext<SanBongContext>(options => options.UseSqlServer(
                builder.Configuration.GetConnectionString("ConnectedDb")
            //Đọc chuỗi keets nối từ file appsettings.json -> ConnectionStrings -> ConnectedDb
                )
            );

            // Đăng ký Repository vào Dependency Injection Container
            builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            // Cấu hình JWT Authentication
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"])),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // ĐOẠN QUAN TRỌNG: Ép Server đọc Token từ Cookie "jwt"
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["jwt"];
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddHttpClient();
            // Đăng ký IConfiguration để dùng được trong _Layout.cshtml
            builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

            //SignalR - Real-Time cập nhật khung giờ
            builder.Services.AddSignalR();
            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.AddHostedService<Web_Stadium.End.MatchmakingAutoCleanupService>();
            builder.Services.AddHostedService<Web_Stadium.End.TournamentBackgroundService>();
            builder.Services.AddScoped<Web_Stadium.Services.EmailService>();
            builder.Services.AddScoped<Web_Stadium.Services.HoanCocService>();
            builder.Services.AddScoped<Web_Stadium.Services.ScheduleService>();
            builder.Services.AddScoped<Web_Stadium.Services.StandingService>();
            builder.Services.AddScoped<Web_Stadium.Services.SuspensionService>();
            builder.Services.AddScoped<Web_Stadium.Services.TournamentNotificationService>();
            builder.Services.AddScoped<Web_Stadium.Services.TournamentExcelService>();
            builder.Services.AddScoped<Web_Stadium.Services.KnockOutService>();
            builder.Services.AddScoped<Web_Stadium.Services.TournamentService>();

            //builder.Services.AddHostedService<Web_Stadium.End.MatchmakingAutoCleanupService>();
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 52428800;
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 52428800;
            });
            // Thêm trước builder.Build()
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 52428800; // 50MB
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 52428800; // 50MB
            });
            var app = builder.Build();

            // === TRACE LIFECYCLE: log mọi lần app start/stop để biết app có bị shutdown sạch không ===
            string logPath = Path.Combine(Directory.GetCurrentDirectory(), "app_lifecycle.log");
            void Trace(string msg)
            {
                try
                {
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
                }
                catch { }
            }
            Trace($"=== APP START === CWD={Directory.GetCurrentDirectory()} PID={Environment.ProcessId}");

            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(() =>
                Trace("ApplicationStopping (someone called shutdown) — stack:\n" + Environment.StackTrace));
            lifetime.ApplicationStopped.Register(() => Trace("ApplicationStopped (process exiting)"));

            AppDomain.CurrentDomain.ProcessExit += (_, __) => Trace("ProcessExit");

            // Log exception trong pipeline, KHÔNG re-throw để không kill process khi client ngắt kết nối.
            app.Use(async (context, next) =>
            {
                try { await next(); }
                catch (Exception ex)
                {
                    Trace($"PIPELINE EXCEPTION at {context.Request.Path}: {ex.GetType().Name} {ex.Message}");
                    try
                    {
                        await System.IO.File.AppendAllTextAsync(
                            Path.Combine(Directory.GetCurrentDirectory(), "crash_log.txt"),
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context.Request.Path}\n{ex}\n\n"
                        );
                    }
                    catch { /* ignore log failure */ }

                    // Nếu response chưa bắt đầu thì trả 500, còn rồi thì thôi.
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Internal Server Error");
                    }
                }
            });
            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            //Map SignalR Hub
            app.MapHub<SanBongHub>("/sanBongHub");
            app.MapHub<Web_Stadium.Hubs.TournamentHub>("/tournamentHub");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
