using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LifeMineralWater.Data;
namespace LifeMineralWater.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CustomerController(ApplicationDbContext context) { _context = context; }
        public IActionResult Index()
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var profile = _context.CustomerProfiles.FirstOrDefault(c => c.UserId == currentUserId);
            if (profile == null) return Content("Customer Profile Map Uninitialized.");
            var ledger = _context.Transactions.Where(t => t.CustomerId == profile.Id).OrderByDescending(t => t.CreatedAt).ToList();
            ViewBag.Profile = profile; return View(ledger);
        }
    }
}