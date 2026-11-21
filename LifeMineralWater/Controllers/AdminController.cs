using System.Security.Claims;
using LifeMineralWater.Common;
using LifeMineralWater.Data;
using LifeMineralWater.Models;
using LifeMineralWater.Models.ViewModels;
using LifeMineralWater.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Controllers;

[Authorize(Roles = AppRoles.Owner)]
public sealed class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IReportService _reports;
    private readonly ILedgerService _ledger;
    private readonly ISettingsService _settings;
    private readonly IRateService _rates;

    public AdminController(ApplicationDbContext db, IReportService reports, ILedgerService ledger,
        ISettingsService settings, IRateService rates)
    {
        _db = db;
        _reports = reports;
        _ledger = ledger;
        _settings = settings;
        _rates = rates;
    }

    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private string Actor => User.Identity?.Name ?? "Owner";

    private void Error(string message) => ModelState.AddModelError(string.Empty, message);

    private async Task<List<SelectListItem>> CustomerOptionsAsync(int? selected = null, bool all = false)
    {
        var items = await _db.Customers.AsNoTracking().Where(x => x.Status == AccountStatus.Active)
            .OrderBy(x => x.Code).ThenBy(x => x.User.FullName)
            .Select(x => new SelectListItem($"{x.Code} — {x.User.FullName}", x.Id.ToString(), x.Id == selected))
            .ToListAsync();
        if (all) items.Insert(0, new SelectListItem("All customers", "", selected is null));
        return items;
    }

    private async Task<List<SelectListItem>> SalesmanOptionsAsync(int? selected = null, bool all = false, bool users = false)
    {
        List<SelectListItem> items;
        if (users)
            items = await _db.Users.AsNoTracking().Where(x => x.Role == AppRoles.Salesman && x.IsActive)
                .OrderBy(x => x.FullName)
                .Select(x => new SelectListItem(x.FullName, x.Id.ToString(), x.Id == selected)).ToListAsync();
        else
            items = await _db.Salesmen.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.Code).ThenBy(x => x.User.FullName)
                .Select(x => new SelectListItem($"{x.Code} — {x.User.FullName}", x.Id.ToString(), x.Id == selected)).ToListAsync();
        if (all) items.Insert(0, new SelectListItem("All salesmen", "", selected is null));
        return items;
    }

    private async Task<List<SelectListItem>> UserOptionsAsync(string role, int? selected = null)
        => await _db.Users.AsNoTracking().Where(x => x.Role == role && x.IsActive)
            .OrderBy(x => x.FullName).ThenBy(x => x.Email)
            .Select(x => new SelectListItem($"{x.FullName} ({x.Email})", x.Id.ToString(), x.Id == selected))
            .ToListAsync();

    // ---------------- Dashboard ----------------
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var requests = await _db.ModificationRequests.AsNoTracking()
            .Where(x => x.Status == RequestStatus.Pending)
            .OrderBy(x => x.RequestedAt)
            .Select(x => new RequestRowDto(x.Id, x.RequestType.ToString(), x.CustomerName,
                x.Reason, x.RequestedAt, x.RequestedBy.FullName))
            .ToListAsync(ct);
        ViewBag.Requests = requests;
        ViewBag.Audits = await _db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Id)
            .Take(8).ToListAsync(ct);
        return View(await _reports.GetDashboardAsync(ct));
    }

    // ---------------- Users (identity only) ----------------
    public async Task<IActionResult> Users(CancellationToken ct)
        => View(await _db.Users.AsNoTracking().OrderBy(x => x.Role).ThenBy(x => x.FullName).ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> UserForm(int? id, CancellationToken ct)
    {
        var vm = new UserEditVm();
        if (id is int uid)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
            if (u is null) return NotFound();
            vm = new UserEditVm { Id = u.Id, FullName = u.FullName, Email = u.Email, Role = u.Role, Phone = u.Phone, IsActive = u.IsActive };
        }
        ViewBag.Roles = AppRoles.All.Select(x => new SelectListItem(x, x, x == vm.Role)).ToList();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UserForm(UserEditVm model, CancellationToken ct)
    {
        if (!AppRoles.All.Contains(model.Role)) Error("Unknown role.");
        if (await _db.Users.AnyAsync(x => x.Email.ToLower() == model.Email.Trim().ToLowerInvariant() && x.Id != model.Id, ct))
            Error("Email already exists.");
        if (model.Id == 0 && string.IsNullOrWhiteSpace(model.Password)) Error("Password is required for a new account.");
        if (!ModelState.IsValid) { ViewBag.Roles = AppRoles.All.Select(x => new SelectListItem(x, x, x == model.Role)).ToList(); return View(model); }

        var user = model.Id == 0 ? new AppUser { CreatedAt = DateTime.UtcNow, CreatedBy = Actor }
            : await _db.Users.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (user is null) return NotFound();
        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim().ToLowerInvariant();
        user.Role = model.Role;
        user.Phone = model.Phone;
        user.IsActive = model.IsActive;
        if (!string.IsNullOrWhiteSpace(model.Password)) user.PasswordHash = PasswordHash.Hash(model.Password);

        if (model.Id == 0) _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Account saved.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(int id, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return NotFound();
        if (user.Id == UserId) { TempData["Error"] = "You cannot deactivate your own account."; return RedirectToAction(nameof(Users)); }
        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Users));
    }

    // ---------------- Customers (domain only) ----------------
    public async Task<IActionResult> Customers(CancellationToken ct)
        => View(await _reports.GetCustomerBalancesAsync(ct));

    [HttpGet]
    public async Task<IActionResult> CustomerForm(int? id, CancellationToken ct)
    {
        var vm = new CustomerEditVm();
        if (id is int cid)
        {
            var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cid, ct);
            if (c is null) return NotFound();
            vm = new CustomerEditVm
            {
                Id = c.Id, Code = c.Code, UserId = c.UserId, AssignedSalesmanId = c.AssignedSalesmanId,
                Phone = c.Phone, Address = c.Address, Status = c.Status,
                OpeningBottleBalance = c.OpeningBottleBalance, OpeningMoneyBalance = c.OpeningMoneyBalance,
                DepositAmount = c.DepositAmount, DepositBottles = c.DepositBottles
            };
        }
        ViewBag.CustomerUsers = await UserOptionsAsync(AppRoles.Customer, vm.UserId);
        ViewBag.Salesmen = await SalesmanOptionsAsync(vm.AssignedSalesmanId);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CustomerForm(CustomerEditVm model, CancellationToken ct)
    {
        if (await _db.Customers.AnyAsync(x => x.Code == model.Code.Trim() && x.Id != model.Id, ct)) Error("Customer code already exists.");
        if (await _db.Customers.AnyAsync(x => x.UserId == model.UserId && x.Id != model.Id, ct)) Error("This account is already mapped to a customer.");
        if (model.AssignedSalesmanId is int sid && !await _db.Salesmen.AnyAsync(x => x.Id == sid && x.IsActive, ct))
            Error("Assigned salesman must be an active salesman.");
        if (!ModelState.IsValid)
        {
            ViewBag.CustomerUsers = await UserOptionsAsync(AppRoles.Customer, model.UserId);
            ViewBag.Salesmen = await SalesmanOptionsAsync(model.AssignedSalesmanId);
            return View(model);
        }

        var customer = model.Id == 0 ? new Customer { CreatedAt = DateTime.UtcNow, CreatedBy = Actor }
            : await _db.Customers.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (customer is null) return NotFound();

        var openingChanged = model.Id != 0 &&
            (customer.OpeningBottleBalance != model.OpeningBottleBalance || customer.OpeningMoneyBalance != model.OpeningMoneyBalance);

        customer.Code = model.Code.Trim();
        customer.UserId = model.UserId;
        customer.AssignedSalesmanId = model.AssignedSalesmanId;
        customer.Phone = model.Phone;
        customer.Address = model.Address;
        customer.Status = model.Status;
        customer.OpeningBottleBalance = model.OpeningBottleBalance;
        customer.OpeningMoneyBalance = model.OpeningMoneyBalance;
        customer.DepositAmount = model.DepositAmount;
        customer.DepositBottles = model.DepositBottles;
        customer.UpdatedAt = DateTime.UtcNow;

        if (model.Id == 0) _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        if (openingChanged) await _ledger.RecalculateCustomerAsync(customer.Id, ct);

        TempData["Toast"] = "Customer saved.";
        return RedirectToAction(nameof(Customers));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCustomer(int id, CancellationToken ct)
    {
        var c = await _db.Customers.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        c.Status = c.Status == AccountStatus.Active ? AccountStatus.Inactive : AccountStatus.Active;
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Customers));
    }

    // ---------------- Salesmen ----------------
    public async Task<IActionResult> Salesmen(CancellationToken ct)
        => View(await _reports.GetSalesmanCashAsync(ct));

    [HttpGet]
    public async Task<IActionResult> SalesmanForm(int? id, CancellationToken ct)
    {
        var vm = new SalesmanEditVm();
        if (id is int sid)
        {
            var s = await _db.Salesmen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid, ct);
            if (s is null) return NotFound();
            vm = new SalesmanEditVm { Id = s.Id, Code = s.Code, UserId = s.UserId, OpeningCash = s.OpeningCash, IsActive = s.IsActive };
        }
        ViewBag.SalesmanUsers = await UserOptionsAsync(AppRoles.Salesman, vm.UserId);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalesmanForm(SalesmanEditVm model, CancellationToken ct)
    {
        if (await _db.Salesmen.AnyAsync(x => x.Code == model.Code.Trim() && x.Id != model.Id, ct)) Error("Salesman code already exists.");
        if (await _db.Salesmen.AnyAsync(x => x.UserId == model.UserId && x.Id != model.Id, ct)) Error("This account is already mapped to a salesman.");
        if (!ModelState.IsValid)
        {
            ViewBag.SalesmanUsers = await UserOptionsAsync(AppRoles.Salesman, model.UserId);
            return View(model);
        }
        var s = model.Id == 0 ? new Salesman { JoinedOn = DateTime.UtcNow.Date }
            : await _db.Salesmen.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (s is null) return NotFound();
        s.Code = model.Code.Trim();
        s.UserId = model.UserId;
        s.OpeningCash = model.OpeningCash;
        if (model.Id == 0) s.CurrentCash = model.OpeningCash;
        s.IsActive = model.IsActive;
        if (model.Id == 0) _db.Salesmen.Add(s);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Salesman saved.";
        return RedirectToAction(nameof(Salesmen));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSalesman(int id, CancellationToken ct)
    {
        var s = await _db.Salesmen.FindAsync(new object[] { id }, ct);
        if (s is null) return NotFound();
        s.IsActive = !s.IsActive;
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Salesmen));
    }

    // ---------------- Staff ----------------
    public async Task<IActionResult> Staff(CancellationToken ct)
        => View(await _db.Staff.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> StaffForm(int? id, CancellationToken ct)
    {
        if (id is not int sid) return View(new StaffEditVm { JoinedOn = DateTime.Today });
        var s = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid, ct);
        if (s is null) return NotFound();
        return View(new StaffEditVm
        {
            Id = s.Id, Code = s.Code, FullName = s.FullName, Designation = s.Designation,
            Phone = s.Phone, MonthlySalary = s.MonthlySalary, IsActive = s.IsActive, JoinedOn = s.JoinedOn
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StaffForm(StaffEditVm model, CancellationToken ct)
    {
        if (await _db.Staff.AnyAsync(x => x.Code == model.Code.Trim() && x.Id != model.Id, ct)) Error("Staff code already exists.");
        if (!ModelState.IsValid) return View(model);
        var s = model.Id == 0 ? new StaffMember { JoinedOn = model.JoinedOn } : await _db.Staff.FindAsync(new object[] { model.Id }, ct);
        if (s is null) return NotFound();
        s.Code = model.Code.Trim(); s.FullName = model.FullName.Trim(); s.Designation = model.Designation;
        s.Phone = model.Phone; s.MonthlySalary = model.MonthlySalary; s.IsActive = model.IsActive;
        if (model.Id == 0) _db.Staff.Add(s);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Staff member saved.";
        return RedirectToAction(nameof(Staff));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStaff(int id, CancellationToken ct)
    {
        var s = await _db.Staff.FindAsync(new object[] { id }, ct);
        if (s is null) return NotFound();
        s.IsActive = !s.IsActive;
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Staff));
    }

    // ---------------- Products & bottle types ----------------
    public async Task<IActionResult> Products(CancellationToken ct)
        => View(await _db.Products.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> ProductForm(int? id, CancellationToken ct)
    {
        if (id is not int pid) return View(new ProductEditVm());
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct);
        if (p is null) return NotFound();
        return View(new ProductEditVm { Id = p.Id, Name = p.Name, Code = p.Code, Description = p.Description, IsActive = p.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductForm(ProductEditVm model, CancellationToken ct)
    {
        if (await _db.Products.AnyAsync(x => (x.Code == model.Code.Trim() || x.Name == model.Name.Trim()) && x.Id != model.Id, ct))
            Error("Product code and name must be unique.");
        if (!ModelState.IsValid) return View(model);
        var p = model.Id == 0 ? new Product() : await _db.Products.FindAsync(new object[] { model.Id }, ct);
        if (p is null) return NotFound();
        p.Code = model.Code.Trim(); p.Name = model.Name.Trim(); p.Description = model.Description; p.IsActive = model.IsActive;
        if (model.Id == 0) _db.Products.Add(p);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Product saved.";
        return RedirectToAction(nameof(Products));
    }

    public async Task<IActionResult> BottleTypes(CancellationToken ct)
        => View(await _db.BottleTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> BottleTypeForm(int? id, CancellationToken ct)
    {
        if (id is not int bid) return View(new BottleTypeEditVm());
        var b = await _db.BottleTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bid, ct);
        if (b is null) return NotFound();
        return View(new BottleTypeEditVm { Id = b.Id, Name = b.Name, CapacityMl = b.CapacityMl, DepositAmount = b.DepositAmount, IsActive = b.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BottleTypeForm(BottleTypeEditVm model, CancellationToken ct)
    {
        if (await _db.BottleTypes.AnyAsync(x => x.Name == model.Name.Trim() && x.Id != model.Id, ct)) Error("Bottle type name already exists.");
        if (!ModelState.IsValid) return View(model);
        var b = model.Id == 0 ? new BottleType() : await _db.BottleTypes.FindAsync(new object[] { model.Id }, ct);
        if (b is null) return NotFound();
        b.Name = model.Name.Trim(); b.CapacityMl = model.CapacityMl; b.DepositAmount = model.DepositAmount; b.IsActive = model.IsActive;
        if (model.Id == 0) _db.BottleTypes.Add(b);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Bottle type saved.";
        return RedirectToAction(nameof(BottleTypes));
    }

    // ---------------- Rates with history ----------------
    public async Task<IActionResult> Rates(CancellationToken ct)
    {
        var rows = await _db.Rates.AsNoTracking()
            .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.Id)
            .Select(r => new RateRowDto(r.Id, r.Customer == null ? "Standard" : r.Customer.User.FullName,
                r.Product == null ? "Any product" : r.Product.Name, r.BottleType == null ? "Any bottle" : r.BottleType.Name,
                r.Amount, r.EffectiveFrom, r.EffectiveTo, r.IsActive))
            .ToListAsync(ct);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> RateForm(int? id, CancellationToken ct)
    {
        var vm = new RateEditVm();
        if (id is int rid)
        {
            var r = await _db.Rates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid, ct);
            if (r is null) return NotFound();
            vm = new RateEditVm { Id = r.Id, CustomerId = r.CustomerId, ProductId = r.ProductId, BottleTypeId = r.BottleTypeId,
                Amount = r.Amount, EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, IsActive = r.IsActive };
        }
        await FillRateOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RateForm(RateEditVm model, CancellationToken ct)
    {
        if (model.EffectiveTo.HasValue && model.EffectiveTo <= model.EffectiveFrom) Error("Effective end must be after effective start.");
        if (!ModelState.IsValid) { await FillRateOptionsAsync(model); return View(model); }

        // Close any overlapping active rule so only one rate is effective at a time.
        if (model.Id == 0 && model.IsActive)
        {
            var overlaps = await _db.Rates.Where(r => r.IsActive && r.CustomerId == model.CustomerId &&
                    r.ProductId == model.ProductId && r.BottleTypeId == model.BottleTypeId &&
                    r.EffectiveFrom <= model.EffectiveFrom && (r.EffectiveTo == null || r.EffectiveTo >= model.EffectiveFrom))
                .ToListAsync(ct);
            foreach (var r in overlaps) { r.EffectiveTo = model.EffectiveFrom.AddSeconds(-1); r.IsActive = false; }
        }

        var rate = model.Id == 0 ? new Rate { CreatedAt = DateTime.UtcNow, CreatedBy = Actor }
            : await _db.Rates.FindAsync(new object[] { model.Id }, ct);
        if (rate is null) return NotFound();
        rate.CustomerId = model.CustomerId; rate.ProductId = model.ProductId; rate.BottleTypeId = model.BottleTypeId;
        rate.Amount = model.Amount; rate.EffectiveFrom = model.EffectiveFrom; rate.EffectiveTo = model.EffectiveTo;
        rate.IsActive = model.IsActive;
        if (model.Id == 0) _db.Rates.Add(rate);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Rate saved. Effective history retained.";
        return RedirectToAction(nameof(Rates));
    }

    private async Task FillRateOptionsAsync(RateEditVm model)
    {
        var customers = await CustomerOptionsAsync(model.CustomerId);
        customers.Insert(0, new SelectListItem("Standard rate (all customers)", "", model.CustomerId is null));
        ViewBag.Customers = customers;
        var products = await _db.Products.AsNoTracking().Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.ProductId)).ToListAsync();
        products.Insert(0, new SelectListItem("Any product", "", model.ProductId is null));
        ViewBag.Products = products;
        var bottles = await _db.BottleTypes.AsNoTracking().Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.BottleTypeId)).ToListAsync();
        bottles.Insert(0, new SelectListItem("Any bottle", "", model.BottleTypeId is null));
        ViewBag.Bottles = bottles;
    }

    // ---------------- Dynamic settings ----------------
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var settings = await _db.SystemSettings.AsNoTracking().OrderBy(x => x.Group).ThenBy(x => x.Key).ToListAsync(ct);
        return View(new SettingEditVm { Settings = settings });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(SettingEditVm model, CancellationToken ct)
    {
        foreach (var posted in model.Settings)
        {
            var entity = await _db.SystemSettings.FindAsync(new object[] { posted.Id }, ct);
            if (entity is null) continue;
            if (entity.Key == "Invoice.DueDay" && (!int.TryParse(posted.Value, out var day) || day is < 1 or > 28))
                Error("Due day must be between 1 and 28.");
            entity.Value = posted.Value ?? string.Empty;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = Actor;
        }
        if (!ModelState.IsValid) return View(model);
        await _db.SaveChangesAsync(ct);
        _settings.Invalidate();
        TempData["Toast"] = "Settings updated and cache refreshed.";
        return RedirectToAction(nameof(Settings));
    }

    public async Task<IActionResult> BankAccounts(CancellationToken ct)
        => View(await _db.BankAccounts.AsNoTracking().OrderByDescending(x => x.IsDefault).ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> BankAccountForm(int? id, CancellationToken ct)
    {
        if (id is not int bid) return View(new BankAccountEditVm());
        var b = await _db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bid, ct);
        if (b is null) return NotFound();
        return View(new BankAccountEditVm { Id = b.Id, BankName = b.BankName, AccountTitle = b.AccountTitle,
            AccountNumber = b.AccountNumber, Iban = b.Iban, Branch = b.Branch, IsDefault = b.IsDefault, IsActive = b.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BankAccountForm(BankAccountEditVm model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var b = model.Id == 0 ? new BankAccount() : await _db.BankAccounts.FindAsync(new object[] { model.Id }, ct);
        if (b is null) return NotFound();
        b.BankName = model.BankName; b.AccountTitle = model.AccountTitle; b.AccountNumber = model.AccountNumber;
        b.Iban = model.Iban; b.Branch = model.Branch; b.IsDefault = model.IsDefault; b.IsActive = model.IsActive;
        if (model.IsDefault)
            foreach (var other in await _db.BankAccounts.Where(x => x.Id != b.Id && x.IsDefault).ToListAsync(ct)) other.IsDefault = false;
        if (model.Id == 0) _db.BankAccounts.Add(b);
        await _db.SaveChangesAsync(ct);
        _settings.Invalidate();
        TempData["Toast"] = "Bank account saved.";
        return RedirectToAction(nameof(BankAccounts));
    }

    public async Task<IActionResult> Announcements(CancellationToken ct)
        => View(await _db.Announcements.AsNoTracking().OrderByDescending(x => x.StartOn).ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> AnnouncementForm(int? id, CancellationToken ct)
    {
        if (id is not int aid) return View(new AnnouncementEditVm { StartOn = DateTime.Today });
        var a = await _db.Announcements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == aid, ct);
        if (a is null) return NotFound();
        return View(new AnnouncementEditVm { Id = a.Id, Title = a.Title, Message = a.Message, Type = a.Type,
            StartOn = a.StartOn, EndOn = a.EndOn, IsActive = a.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AnnouncementForm(AnnouncementEditVm model, CancellationToken ct)
    {
        if (model.EndOn.HasValue && model.EndOn < model.StartOn) Error("End date cannot be before start date.");
        if (!ModelState.IsValid) return View(model);
        var a = model.Id == 0 ? new Announcement { CreatedAt = DateTime.UtcNow, CreatedBy = Actor }
            : await _db.Announcements.FindAsync(new object[] { model.Id }, ct);
        if (a is null) return NotFound();
        a.Title = model.Title; a.Message = model.Message; a.Type = model.Type;
        a.StartOn = model.StartOn; a.EndOn = model.EndOn; a.IsActive = model.IsActive;
        if (model.Id == 0) _db.Announcements.Add(a);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Announcement saved.";
        return RedirectToAction(nameof(Announcements));
    }

    // ---------------- Transactions & requests ----------------
    public async Task<IActionResult> Transactions(DateTime? from, DateTime? to, int? customerId, int? salesmanId,
        int page = 1, CancellationToken ct = default)
    {
        ViewBag.Customers = await CustomerOptionsAsync(customerId, true);
        ViewBag.Salesmen = await SalesmanOptionsAsync(salesmanId, true);
        return View(new SalesmanHistoryVm
        {
            Rows = await _reports.GetAllLedgerAsync(customerId, salesmanId, from, to, page, 30, ct),
            From = from, To = to, Page = page
        });
    }

    [HttpGet]
    public async Task<IActionResult> EditTransaction(int id, CancellationToken ct)
    {
        var e = await _db.LedgerEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return NotFound();
        var vm = new DeliveryInput
        {
            CustomerId = e.CustomerId, ProductId = e.ProductId ?? 0, BottleTypeId = e.BottleTypeId ?? 0,
            FilledDelivered = e.FilledDelivered, EmptyReceived = e.EmptyReceived,
            AmountReceived = e.AmountReceived, Description = e.Description, EntryDate = e.EntryDate
        };
        ViewBag.EntryId = id;
        ViewBag.Products = await _db.Products.AsNoTracking().Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == vm.ProductId)).ToListAsync(ct);
        ViewBag.Bottles = await _db.BottleTypes.AsNoTracking().Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == vm.BottleTypeId)).ToListAsync(ct);
        ViewBag.CustomerName = await _db.Customers.AsNoTracking().Where(x => x.Id == e.CustomerId)
            .Select(x => x.User.FullName).FirstOrDefaultAsync(ct);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTransaction(int id, DeliveryInput model, CancellationToken ct)
    {
        var result = await _ledger.EditEntryAsync(id, model, UserId, ct);
        if (!result.Succeeded) { Error(result.Error!); return RedirectToAction(nameof(EditTransaction), new { id }); }
        TempData["Toast"] = "Transaction corrected; balances recalculated and audit recorded.";
        return RedirectToAction(nameof(Transactions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransaction(int id, CancellationToken ct)
    {
        var result = await _ledger.DeleteEntryAsync(id, UserId, ct);
        TempData[result.Succeeded ? "Toast" : "Error"] = result.Succeeded
            ? "Transaction deleted (soft delete); balances recalculated and audit recorded."
            : result.Error;
        return RedirectToAction(nameof(Transactions));
    }

    [HttpGet]
    public async Task<IActionResult> AdjustBalance(int? customerId, CancellationToken ct)
    {
        ViewBag.Customers = await CustomerOptionsAsync(customerId);
        return View(new AdjustmentInput { CustomerId = customerId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustBalance(AdjustmentInput model, CancellationToken ct)
    {
        if (model.BottleCorrection == 0 && model.AmountAdjusted == 0 && model.AmountReceived == 0)
            Error("Provide a bottle or money correction.");
        if (string.IsNullOrWhiteSpace(model.Description)) Error("Reason is required.");
        if (!ModelState.IsValid) { ViewBag.Customers = await CustomerOptionsAsync(model.CustomerId); return View(model); }
        var result = await _ledger.AdjustAsync(model, UserId, ct);
        if (!result.Succeeded) { Error(result.Error!); ViewBag.Customers = await CustomerOptionsAsync(model.CustomerId); return View(model); }
        TempData["Toast"] = "Adjustment posted to ledger.";
        return RedirectToAction(nameof(Transactions));
    }

    public async Task<IActionResult> Requests(CancellationToken ct)
        => View(await _db.ModificationRequests.AsNoTracking()
            .OrderByDescending(x => x.RequestedAt)
            .Select(x => new RequestDetailDto(x.Id, x.RequestType.ToString(), x.Status.ToString(),
                x.CustomerName, x.Reason, x.RequestedFilled, x.RequestedEmpty,
                x.RequestedAmountReceived, x.RequestedRate, x.RequestedBy.FullName, x.RequestedAt, x.ReviewNote))
            .ToListAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(int id, string? note, CancellationToken ct)
    {
        var result = await _ledger.ApproveRequestAsync(id, UserId, note, ct);
        TempData[result.Succeeded ? "Toast" : "Error"] = result.Succeeded
            ? "Request approved and ledger recalculated." : result.Error;
        return RedirectToAction(nameof(Requests));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(int id, string? note, CancellationToken ct)
    {
        var result = await _ledger.RejectRequestAsync(id, UserId, note, ct);
        TempData[result.Succeeded ? "Toast" : "Error"] = result.Succeeded ? "Request rejected." : result.Error;
        return RedirectToAction(nameof(Requests));
    }

    // ---------------- Finance ----------------
    public async Task<IActionResult> CashBank(CancellationToken ct)
    {
        var vm = new AdminFinanceVm
        {
            Position = await _reports.GetCashBankAsync(ct),
            CashEntries = await _db.CashBookEntries.AsNoTracking().OrderByDescending(x => x.TransactionDate).Take(50).ToListAsync(ct),
            BankEntries = await _db.BankBookEntries.AsNoTracking().OrderByDescending(x => x.TransactionDate).Take(50).ToListAsync(ct),
            Banks = await _settings.GetBankAccountsAsync(),
            Salesmen = await SalesmanOptionsAsync(null, false, true)
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCash(CashEntryVm model, CancellationToken ct)
    {
        if (model.Amount <= 0) Error("Amount must be positive.");
        if (!ModelState.IsValid) return RedirectToAction(nameof(CashBank));
        if (model.Direction == CashDirection.In && model.SalesmanId is int sid)
        {
            var s = await _db.Salesmen.FindAsync(new object[] { sid }, ct);
            if (s is not null) s.CurrentCash += model.Amount;
        }
        _db.CashBookEntries.Add(new CashBookEntry
        {
            SalesmanId = model.SalesmanId, Direction = model.Direction, Category = model.Category,
            Amount = model.Amount, TransactionDate = model.TransactionDate ?? DateTime.UtcNow,
            Description = model.Description, CreatedBy = Actor, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Cash book entry posted.";
        return RedirectToAction(nameof(CashBank));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBank(CashEntryVm model, CancellationToken ct)
    {
        if (model.Amount <= 0) Error("Amount must be positive.");
        if (model.BankAccountId is null || !await _db.BankAccounts.AnyAsync(x => x.Id == model.BankAccountId, ct)) Error("Select a bank account.");
        if (!ModelState.IsValid) return RedirectToAction(nameof(CashBank));
        _db.BankBookEntries.Add(new BankBookEntry
        {
            BankAccountId = model.BankAccountId!.Value, Direction = model.Direction, Amount = model.Amount,
            TransactionDate = model.TransactionDate ?? DateTime.UtcNow, Reference = model.Category.ToString(),
            Description = model.Description, CreatedBy = Actor, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Bank book entry posted.";
        return RedirectToAction(nameof(CashBank));
    }

    public async Task<IActionResult> Expenses(int page = 1, CancellationToken ct = default)
        => View(await _reports.GetExpensesAsync(null, null, page, 30, ct));

    public async Task<IActionResult> Payroll(CancellationToken ct)
    {
        ViewBag.Staff = await _db.Staff.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString())).ToListAsync(ct);
        return View(await _db.PayrollEntries.AsNoTracking().OrderByDescending(x => x.Period)
            .Select(x => new PayrollRowDto(x.Id, x.Staff.FullName, x.Period, x.BasicSalary, x.Allowances, x.Deductions, x.NetPay, x.Status.ToString(), x.PaidOn))
            .ToListAsync(ct));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Payroll(PayrollEditVm model, CancellationToken ct)
    {
        if (!await _db.Staff.AnyAsync(x => x.Id == model.StaffId && x.IsActive, ct)) Error("Select an active staff member.");
        if (await _db.PayrollEntries.AnyAsync(x => x.StaffId == model.StaffId && x.Period == model.Period && x.Id != model.Id, ct))
            Error("Payroll for this staff and period already exists.");
        if (!ModelState.IsValid) return RedirectToAction(nameof(Payroll));

        var p = model.Id == 0 ? new PayrollEntry { RecordedByUserId = UserId }
            : await _db.PayrollEntries.FindAsync(new object[] { model.Id }, ct);
        if (p is null) return NotFound();
        p.StaffId = model.StaffId; p.Period = model.Period; p.BasicSalary = model.BasicSalary;
        p.Allowances = model.Allowances; p.Deductions = model.Deductions; p.Status = model.Status;
        if (model.Status == PayrollStatus.Paid && p.PaidOn is null) p.PaidOn = DateTime.UtcNow;
        if (model.Status == PayrollStatus.Unpaid) p.PaidOn = null;
        if (model.Id == 0) _db.PayrollEntries.Add(p);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Payroll saved.";
        return RedirectToAction(nameof(Payroll));
    }

    public async Task<IActionResult> Suppliers(CancellationToken ct)
        => View(await _db.Suppliers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> SupplierForm(int? id, CancellationToken ct)
    {
        if (id is not int sid) return View(new SupplierEditVm());
        var s = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid, ct);
        if (s is null) return NotFound();
        return View(new SupplierEditVm { Id = s.Id, Name = s.Name, Phone = s.Phone, Address = s.Address,
            OpeningBalance = s.OpeningBalance, IsActive = s.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SupplierForm(SupplierEditVm model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var s = model.Id == 0 ? new Supplier { CreatedAt = DateTime.UtcNow }
            : await _db.Suppliers.FindAsync(new object[] { model.Id }, ct);
        if (s is null) return NotFound();
        s.Name = model.Name; s.Phone = model.Phone; s.Address = model.Address;
        s.OpeningBalance = model.OpeningBalance;
        if (model.Id == 0) s.CurrentBalance = model.OpeningBalance;
        s.IsActive = model.IsActive;
        if (model.Id == 0) _db.Suppliers.Add(s);
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Supplier saved.";
        return RedirectToAction(nameof(Suppliers));
    }

    [HttpGet]
    public async Task<IActionResult> SupplierLedger(int id, CancellationToken ct)
    {
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null) return NotFound();
        ViewBag.Supplier = supplier;
        return View(await _db.SupplierLedgerEntries.AsNoTracking().Where(x => x.SupplierId == id)
            .OrderByDescending(x => x.TransactionDate).ToListAsync(ct));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SupplierEntry(SupplierEntryVm model, CancellationToken ct)
    {
        var supplier = await _db.Suppliers.FindAsync(new object[] { model.SupplierId }, ct);
        if (supplier is null || model.Amount <= 0) { Error("Invalid supplier entry."); return RedirectToAction(nameof(Suppliers)); }
        var when = model.TransactionDate ?? DateTime.UtcNow;
        var delta = model.EntryType == SupplierEntryType.Payment ? -model.Amount : model.Amount;
        supplier.CurrentBalance += delta;
        _db.SupplierLedgerEntries.Add(new SupplierLedgerEntry
        {
            SupplierId = supplier.Id, EntryType = model.EntryType, Amount = model.Amount,
            BalanceAfter = supplier.CurrentBalance, TransactionDate = when,
            Description = model.Description, CreatedBy = Actor, CreatedAt = DateTime.UtcNow
        });
        if (model.EntryType == SupplierEntryType.Payment)
            _db.CashBookEntries.Add(new CashBookEntry
            {
                Direction = CashDirection.Out, Category = CashCategory.Other, Amount = model.Amount,
                TransactionDate = when, Description = $"Supplier payment: {supplier.Name}", CreatedBy = Actor, CreatedAt = DateTime.UtcNow
            });
        await _db.SaveChangesAsync(ct);
        TempData["Toast"] = "Supplier ledger entry posted.";
        return RedirectToAction(nameof(SupplierLedger), new { id = supplier.Id });
    }

    public async Task<IActionResult> Receivables(CancellationToken ct)
        => View(await _reports.GetReceivablesPayablesAsync(ct));

    // ---------------- Reports (single projected grid, many report types) ----------------
    public async Task<IActionResult> Reports(string? report, DateTime? from, DateTime? to, int? customerId,
        int? salesmanId, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(report) ? "sales" : report.ToLowerInvariant();
        var vm = new AdminReportVm
        {
            Title = key switch
            {
                "bottles" => "Filled & Empty Bottle Report",
                "balances" => "Customer Bottle Balances",
                "money" => "Customer Money / Credit Balances",
                "collections" => "Cash Collections",
                "expenses" => "Business Expenses",
                "salesman" => "Salesman Cash Balances",
                "receivables" => "Receivables & Payables",
                "profit" => "Profit & Loss",
                "positions" => "Cash & Bank Positions",
                "customers" => "Customer-wise Sales",
                _ => "Salesman-wise / Detailed Sales"
            },
            From = from, To = to, CustomerId = customerId, SalesmanId = salesmanId,
            Customers = await CustomerOptionsAsync(customerId, true),
            Salesmen = await SalesmanOptionsAsync(salesmanId, true)
        };

        switch (key)
        {
            case "bottles":
                vm.Headers = new List<string> { "Date", "Customer", "Salesman", "Filled (+)", "Empty (-)", "Net", "Balance After" };
                var b = await _reports.GetAllLedgerAsync(customerId, salesmanId, from, to, 1, 200, ct);
                vm.Rows = b.Items.Select(x => new List<string> { x.EntryDate.ToString("dd-MM-yyyy"), x.CustomerName, x.EntryType,
                    x.Filled.ToString(), x.Empty.ToString(), x.BottleDelta.ToString(), x.BottleBalanceAfter.ToString() }).ToList();
                break;
            case "balances":
            case "money":
                vm.Headers = key == "balances"
                    ? new List<string> { "Code", "Customer", "Salesman", "Bottle Balance", "Deposit" }
                    : new List<string> { "Code", "Customer", "Status", "Money Balance" };
                var cs = await _reports.GetCustomerBalancesAsync(ct);
                vm.Rows = cs.Select(x => key == "balances"
                    ? new List<string> { x.Code, x.Name, x.Salesman, x.BottleBalance.ToString(), $"{x.DepositAmount:N2} / {x.DepositBottles}" }
                    : new List<string> { x.Code, x.Name, x.MoneyBalance >= 0 ? "Receivable" : "Advance", $"{x.MoneyBalance:N2}" }).ToList();
                break;
            case "collections":
                vm.Headers = new List<string> { "Date", "Customer", "Salesman", "Collected" };
                var col = await _reports.GetAllLedgerAsync(customerId, salesmanId, from, to, 1, 200, ct);
                vm.Rows = col.Items.Where(x => x.Received > 0)
                    .Select(x => new List<string> { x.EntryDate.ToString("dd-MM-yyyy"), x.CustomerName, x.EntryType, x.Received.ToString("N2") }).ToList();
                break;
            case "expenses":
                vm.Headers = new List<string> { "Date", "Category", "Description", "Recorded By", "Amount" };
                var ex = await _reports.GetExpensesAsync(from, to, 1, 200, ct);
                vm.Rows = ex.Items.Select(x => new List<string> { x.Date.ToString("dd-MM-yyyy"), x.Category, x.Description, x.RecordedBy, x.Amount.ToString("N2") }).ToList();
                break;
            case "salesman":
                vm.Headers = new List<string> { "Salesman", "Opening Cash", "Closing Cash", "Status" };
                var sm = await _reports.GetSalesmanCashAsync(ct);
                vm.Rows = sm.Select(x => new List<string> { x.Name, x.OpeningCash.ToString("N2"), x.CurrentCash.ToString("N2"), x.IsActive ? "Active" : "Inactive" }).ToList();
                break;
            case "receivables":
                vm.Headers = new List<string> { "Party", "Bottles", "Balance", "Type" };
                var rc = await _reports.GetReceivablesPayablesAsync(ct);
                vm.Rows = rc.Select(x => new List<string> { x.Name, x.BottleBalance.ToString(), x.MoneyBalance.ToString("N2"), x.Type }).ToList();
                break;
            case "profit":
                vm.Headers = new List<string> { "Sales", "Expenses", "Payroll", "Net Profit", "Period" };
                var f = from ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var t = to ?? DateTime.UtcNow;
                var pl = await _reports.GetProfitLossAsync(f, t, ct);
                vm.Rows = new List<List<string>> { new() { pl.Sales.ToString("N2"), pl.Expenses.ToString("N2"), pl.Payroll.ToString("N2"),
                    pl.Profit.ToString("N2"), $"{pl.From:dd-MM-yyyy} .. {pl.To:dd-MM-yyyy}" } };
                break;
            case "positions":
                vm.Headers = new List<string> { "Book", "In", "Out", "Position" };
                var pos = await _reports.GetCashBankAsync(ct);
                vm.Rows = new List<List<string>>
                {
                    new() { "Cash", pos.CashIn.ToString("N2"), pos.CashOut.ToString("N2"), pos.CashPosition.ToString("N2") },
                    new() { "Bank", pos.BankIn.ToString("N2"), pos.BankOut.ToString("N2"), pos.BankPosition.ToString("N2") }
                };
                break;
            case "customers":
                vm.Headers = new List<string> { "Date", "Customer", "Filled", "Amount", "Collected", "Balance" };
                var cw = await _reports.GetSalesAsync(customerId, null, from, to, 1, 200, ct);
                vm.Rows = cw.Items.Select(x => new List<string> { x.Date.ToString("dd-MM-yyyy"), x.Customer, x.Filled.ToString(),
                    x.SaleAmount.ToString("N2"), x.Received.ToString("N2"), x.BalanceAfter.ToString("N2") }).ToList();
                break;
            default:
                vm.Headers = new List<string> { "Date", "Customer", "Salesman", "Filled", "Empty", "Rate", "Sales", "Collected", "Balance" };
                var sales = await _reports.GetSalesAsync(customerId, salesmanId, from, to, 1, 200, ct);
                vm.Rows = sales.Items.Select(x => new List<string> { x.Date.ToString("dd-MM-yyyy"), x.Customer, x.Salesman,
                    x.Filled.ToString(), x.Empty.ToString(), x.Rate.ToString("N2"), x.SaleAmount.ToString("N2"),
                    x.Received.ToString("N2"), x.BalanceAfter.ToString("N2") }).ToList();
                break;
        }
        return View(vm);
    }

    public async Task<IActionResult> Audit(int page = 1, CancellationToken ct = default)
    {
        var pageSize = 30;
        var total = await _db.AuditLogs.AsNoTracking().CountAsync(ct);
        var rows = await _db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(rows);
    }
}

public record RateRowDto(int Id, string Customer, string Product, string Bottle, decimal Amount,
    DateTime From, DateTime? To, bool Active);

public record RequestRowDto(int Id, string Type, string Customer, string Reason, DateTime At, string RequestedBy);

public record RequestDetailDto(int Id, string Type, string Status, string Customer, string Reason,
    int Filled, int Empty, decimal Received, decimal Rate, string RequestedBy, DateTime At, string? ReviewNote);

public record PayrollRowDto(int Id, string Staff, DateTime Period, decimal Basic, decimal Allowances,
    decimal Deductions, decimal Net, string Status, DateTime? PaidOn);
