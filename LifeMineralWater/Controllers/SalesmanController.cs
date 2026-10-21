using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LifeMineralWater.Data;
using LifeMineralWater.Models;
namespace LifeMineralWater.Controllers
{
    [Authorize(Roles = "Salesman")]
    public class SalesmanController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SalesmanController(ApplicationDbContext context) { _context = context; }
        public IActionResult Index()
        {
            var customers = _context.CustomerProfiles.Include(c => c.User).ToList(); return
        View(customers);
        }
        [HttpGet]
        public IActionResult RecordTransaction(int customerId)
        {
            var customer = _context.CustomerProfiles.Include(c =>
        c.User).FirstOrDefault(x => x.Id == customerId); var trans = new Transaction { CustomerId = customerId }; ViewBag.CustomerName
        = customer?.User?.Name; ViewBag.Rate = customer?.StandardRate; return View(trans);
        }
        [HttpPost]
        public IActionResult RecordTransaction(Transaction t, decimal activeRate)
        {
            var customer = _context.CustomerProfiles.Find(t.CustomerId);
            if (customer != null)
            {
                t.SalesmanId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                t.CreatedBy = User.Identity?.Name ?? "Salesman"; t.TotalBill = t.FilledDelivered * activeRate;
                // --- CRITICAL BACKEND DUAL BALANCE ACCOUNTING CALCULATIONS --
                customer.BottleBalance = (customer.BottleBalance + t.FilledDelivered) - t.EmptyReceived;
                customer.MoneyBalance = (customer.MoneyBalance + t.TotalBill) - t.AmountPaid;
                _context.Transactions.Add(t); _context.SaveChanges(); return RedirectToAction("Index");
            }
            return View(t);
        }
    }
}