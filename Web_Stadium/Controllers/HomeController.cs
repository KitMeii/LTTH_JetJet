using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Models;

namespace Web_Stadium.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SanBongContext _context;

        public HomeController(ILogger<HomeController> logger, SanBongContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Giải đấu nổi bật — đang RegistrationOpen hoặc Active
            ViewBag.GiaiNoiBat = await _context.GiaiDaus
                .Include(g => g.SanBong)
                .Include(g => g.DoiBongs)
                .Where(g => g.TrangThai == "RegistrationOpen"
                         || g.TrangThai == "Active")
                .OrderByDescending(g => g.NgayBatDau)
                .Take(6)
                .ToListAsync();

            // Thống kê cho Hero
            ViewBag.TongGiai = await _context.GiaiDaus
                .CountAsync(g => g.TrangThai != "Draft");
            ViewBag.GiaiDangDienRa = await _context.GiaiDaus
                .CountAsync(g => g.TrangThai == "Active");

            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
            => View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
    }
}