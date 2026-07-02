using Microsoft.AspNetCore.Mvc;
using Web_Stadium.EFCore;

// Cài package: dotnet add package BCrypt.Net-Next
using BCrypt.Net;

namespace Web_Stadium.Controllers
{
    public class AuthController : Controller
    {
        private readonly IRepository<User> _userRepo;
        private readonly IConfiguration _config;

        public AuthController(IRepository<User> userRepo, IConfiguration config)
        {
            _userRepo = userRepo;
            _config = config;
        }

        // GET /Auth/Login
        public IActionResult Login()
        {
            if (Request.Cookies["jwt"] != null)
                return RedirectToAction("Index", "Home");
            return View();
        }

        // POST /Auth/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string matkhau)
        {
            // Bước 1: tìm theo email trước (không query theo password nữa)
            var user = await _userRepo.FirstOrDefaultAsync(u => u.Email == email);

            // Bước 2: kiểm tra mật khẩu bằng BCrypt
            if (user == null || !BCrypt.Net.BCrypt.Verify(matkhau, user.MatKhau))
            {
                ViewBag.Error = "Email hoặc mật khẩu không đúng.";
                return View();
            }

            // Bước 3: kiểm tra IsActive
            if (!user.IsActive)
            {
                ViewBag.Error = user.VaiTro == "Owner"
                    ? "Tài khoản đang chờ Admin phê duyệt. Vui lòng chờ thông báo."
                    : "Tài khoản đã bị khoá. Vui lòng liên hệ Admin.";
                return View();
            }

            // Bước 4: cấp JWT
            var token = TokenHelper.TaoToken(user, _config);
            var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

            Response.Cookies.Append("jwt", token, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.Now.AddHours(24),
                Path = "/",
                Secure = !isDev,
                SameSite = SameSiteMode.Lax
            });

            return user.VaiTro switch
            {
                "Admin" => RedirectToAction("Index", "Admin"),
                "Owner" => RedirectToAction("Index", "Owner"),
                "Staff" => RedirectToAction("Index", "Staff"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        // GET /Auth/Register
        public IActionResult Register()
        {
            if (Request.Cookies["jwt"] != null)
                return RedirectToAction("Index", "Home");
            return View();
        }

        // POST /Auth/Register
        [HttpPost]
        public async Task<IActionResult> Register(
            string ho, string ten,
            string email, string matKhau,
            string soDienThoai, string vaiTro)
        {
            // Kiểm tra email đã tồn tại
            var existing = await _userRepo.FirstOrDefaultAsync(u => u.Email == email);
            if (existing != null)
            {
                ViewBag.Error = "Email này đã được đăng ký.";
                return View();
            }

            // Chỉ cho phép đăng ký 2 role: User hoặc Owner
            // Whitelist cứng — không tin tham số từ client
            var roleHopLe = new[] { "User", "Owner" };
            var roleCuoi = roleHopLe.Contains(vaiTro) ? vaiTro : "User";

            var user = new User
            {
                HoTen = $"{ho} {ten}".Trim(),
                Email = email,
                // Hash mật khẩu bằng BCrypt trước khi lưu
                MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhau),
                SoDienThoai = soDienThoai,
                VaiTro = roleCuoi,
                // Owner mới = IsActive false, chờ Admin phê duyệt sân đầu tiên
                // User thường = IsActive true ngay
                IsActive = roleCuoi != "Owner",
                NgayTao = DateTime.Now
            };

            await _userRepo.AddAsync(user);

            TempData["Success"] = roleCuoi == "Owner"
                ? "Đăng ký thành công! Tài khoản Owner cần Admin phê duyệt trước khi đăng nhập."
                : "Đăng ký thành công! Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        // GET /Auth/Logout
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt", new CookieOptions { Path = "/" });
            TempData["Success"] = "Đã đăng xuất thành công!";
            return RedirectToAction("Login");
        }
    }
}