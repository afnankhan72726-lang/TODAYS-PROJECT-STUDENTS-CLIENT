using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using LifeMineralWater.Data;
using LifeMineralWater.Models;
namespace LifeMineralWater.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public AdminController(ApplicationDbContext context, IWebHostEnvironment env) { _context = context; _env = env; }
        public IActionResult Dashboard()
        {
            ViewBag.TotalCustomers = _context.CustomerProfiles.Count(); ViewBag.TotalReceivables =
        _context.CustomerProfiles.Sum(c => c.MoneyBalance); return View();
        }
        [HttpGet]
        public IActionResult CreateCustomer()
        {
            ViewBag.Users = _context.Users.Where(u => u.Role.Name == "Customer").ToList(); return
        View();
        }
        [HttpPost]
        public IActionResult CreateCustomer(CustomerProfile profile, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                // Manual directory context streaming file upload validation
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create)) { imageFile.CopyTo(fileStream); }
                    profile.ProfilePicturePath = "/uploads/" + uniqueFileName;
                }
                _context.CustomerProfiles.Add(profile); _context.SaveChanges(); return RedirectToAction("Dashboard");
            }
            ViewBag.Users = _context.Users.Where(u => u.Role.Name == "Customer").ToList(); return View(profile);
        }
        [HttpPost]
        public IActionResult ApproveModification(int transactionId, int correctedFilled, int correctedEmpty, decimal correctedPaid)
        {
            var trans = _context.Transactions.Include(t => t.Customer).FirstOrDefault(x => x.Id == transactionId);
            if (trans != null && trans.Customer != null)
            {
                // 1. Rollback historical values to isolate tracking parameters
                trans.Customer.BottleBalance -= (trans.FilledDelivered - trans.EmptyReceived);
                trans.Customer.MoneyBalance -= (trans.TotalBill - trans.AmountPaid);
                // 2. Overwrite structural row instances
                trans.FilledDelivered = correctedFilled; trans.EmptyReceived = correctedEmpty; trans.AmountPaid = correctedPaid;
                trans.IsPendingModification = false;
                // 3. Re-execute live balance workflows
                trans.Customer.BottleBalance += (correctedFilled - correctedEmpty); trans.Customer.MoneyBalance += (trans.TotalBillcorrectedPaid);
                _context.SaveChanges();
            }
            return RedirectToAction("Dashboard");
        }
    }
}