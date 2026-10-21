using LifeMineralWater.Data;
using LifeMineralWater.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LifeMineralWater.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AccountController(ApplicationDbContext context) { _context = context; }

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.Roles = _context.Roles.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult Register(User user)
        {
            if (ModelState.IsValid)
            {
                _context.Users.Add(user);
                _context.SaveChanges();
                return RedirectToAction("Login");
            }
            ViewBag.Roles = _context.Roles.ToList();
            return View(user);
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.Email == email && u.Password == password);
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Role, user.Role.Name)
                };

                var identity = new ClaimsIdentity(claims, "LifeWaterCookie");
                var principal = new ClaimsPrincipal(identity);

                HttpContext.SignInAsync("LifeWaterCookie", principal).Wait();

                if (user.Role.Name == "Admin") return RedirectToAction("Dashboard", "Admin");
                if (user.Role.Name == "Salesman") return RedirectToAction("Index", "Salesman");
                return RedirectToAction("Index", "Customer");
            }
            ViewBag.Error = "Galt credential hain!";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.SignOutAsync("LifeWaterCookie").Wait();
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied() => View();
    }
}
