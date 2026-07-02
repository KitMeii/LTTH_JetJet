using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Web_Stadium.EFCore;

namespace Web_Stadium.Controllers
{
    public static class TokenHelper
    {
        // 1. Tạo JWT Token từ thông tin User
        public static string TaoToken(User user, IConfiguration config)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT:Secret"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("UserId", user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                // Sử dụng nhãn chuẩn để Layout dễ đọc
                new Claim(ClaimTypes.Role, user.VaiTro ?? "User"),
                new Claim(ClaimTypes.Name, user.HoTen ?? "Thành viên")
            };

            var token = new JwtSecurityToken(
                claims: claims,
                // Dùng DateTime.Now để tránh lệch múi giờ Utc khiến token hết hạn ngay
                expires: DateTime.Now.AddHours(int.Parse(config["JWT:ExpireHours"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 2. Đọc thông tin từ Token
        public static ClaimsPrincipal? DocToken(string token, IConfiguration config)
        {
            try
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT:Secret"]!));
                var handler = new JwtSecurityTokenHandler();

                return handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero // Kiểm tra thời gian chính xác tuyệt đối
                }, out _);
            }
            catch { return null; }
        }

        // 3. Lấy UserId từ Cookie JWT
        public static int? LayUserId(HttpRequest request, IConfiguration config)
        {
            var token = request.Cookies["jwt"];
            if (string.IsNullOrEmpty(token)) return null;
            var principal = DocToken(token, config);
            return principal?.FindFirst("UserId") != null ? int.Parse(principal.FindFirst("UserId")!.Value) : null;
        }
    }
}