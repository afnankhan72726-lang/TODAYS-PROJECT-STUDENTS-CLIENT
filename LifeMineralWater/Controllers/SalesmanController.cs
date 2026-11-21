using System.Security.Claims;
using FluentValidation;
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

[Authorize(Roles = AppRoles.Salesman)]
public sealed class SalesmanController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILedgerService _ledger;
    private readonly IReportService _reports;
    private readonly IRateService _rates;
    private readonly ISettingsService _settings;
    private readonly IValidator<DeliveryInput> _deliveryValidator;
    private readonly IValidator<PaymentInput> _paymentValidator;
    private readonly IValidator<ExpenseInput> _expenseValidator;
    private readonly IValidator<AdvanceInput> _advanceValidator;
    private readonly IValidator<ChangeRequestInput> _requestValidator;

    public SalesmanController(ApplicationDbContext db, ILedgerService ledger, IReportService reports,
        IRateService rates, ISettingsService settings, IValidator<DeliveryInput> deliveryValidator,
        IValidator<PaymentInput> paymentValidator, IValidator<ExpenseInput> expenseValidator,
        IValidator<AdvanceInput> advanceValidator, IValidator<ChangeRequestInput> requestValidator)
    {
        _db = db;
        _ledger = ledger;
        _reports = reports;
        _rates = rates;
        _settings = settings;
        _deliveryValidator = deliveryValidator;
        _paymentValidator = paymentValidator;
        _expenseValidator = expenseValidator;
        _advanceValidator = advanceValidator;
        _requestValidator = requestValidator;
    }

    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private async Task<Salesman?> CurrentSalesmanAsync(CancellationToken ct)
        => await _db.Salesmen.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId && x.IsActive, ct);

    private static string DepositText(decimal amount, int bottles, string paid, string none)
        => bottles > 0 || amount > 0
            ? paid.Replace("{Amount}", amount.ToString("N2")).Replace("{Bottles}", bottles.ToString())
            : none;

    private async Task<bool> ApplyValidation<T>(IValidator<T> validator, T model)
    {
        var result = await validator.ValidateAsync(model);
        foreach (var error in result.Errors) ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return result.IsValid;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var salesman = await CurrentSalesmanAsync(ct);
        if (salesman is null) return RedirectToAction("AccessDenied", "Account");

        var paid = await _settings.GetAsync("Deposit.PaidTemplate");
        var none = await _settings.GetAsync("Deposit.NoneMessage");
        var customers = await _db.Customers.AsNoTracking()
            .Where(x => x.AssignedSalesmanId == salesman!.Id && x.Status == AccountStatus.Active)
            .Select(x => new CustomerPickVm(x.Id, x.Code, x.User.FullName, x.BottleBalance, x.MoneyBalance,
                DepositText(x.DepositAmount, x.DepositBottles, paid, none)))
            .ToListAsync(ct);

        return View(new SalesmanDashboardVm
        {
            Customers = customers,
            OpeningCash = salesman!.OpeningCash,
            CurrentCash = salesman.CurrentCash,
            Today = await _reports.GetEndOfDayAsync(UserId, DateTime.UtcNow.Date, ct)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Delivery(int customerId, CancellationToken ct)
    {
        var salesman = await CurrentSalesmanAsync(ct);
        if (salesman is null) return RedirectToAction("AccessDenied", "Account");
        var paid = await _settings.GetAsync("Deposit.PaidTemplate");
        var none = await _settings.GetAsync("Deposit.NoneMessage");
        var customer = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == customerId && x.AssignedSalesmanId == salesman.Id && x.Status == AccountStatus.Active)
            .Select(x => new CustomerPickVm(x.Id, x.Code, x.User.FullName, x.BottleBalance, x.MoneyBalance,
                DepositText(x.DepositAmount, x.DepositBottles, paid, none)))
            .FirstOrDefaultAsync(ct);
        if (customer is null) return RedirectToAction("AccessDenied", "Account");

        var products = await _db.Products.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(ct);
        var bottles = await _db.BottleTypes.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(ct);
        var rate = await _rates.ResolveAsync(customer.Id, products.FirstOrDefault()?.Value is string p && int.TryParse(p, out var pi) ? pi : null,
            bottles.FirstOrDefault()?.Value is string b && int.TryParse(b, out var bi) ? bi : null, DateTime.UtcNow, ct);

        return View(new DeliveryFormVm
        {
            Input = new DeliveryInput { CustomerId = customer.Id, ProductId = int.Parse(products[0].Value), BottleTypeId = int.Parse(bottles[0].Value), EntryDate = DateTime.Today },
            Customer = customer, Rate = rate, Products = products, BottleTypes = bottles
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delivery([Bind(Prefix = "Input")] DeliveryInput model, CancellationToken ct)
    {
        if (!await ApplyValidation(_deliveryValidator, model)) return await RebuildDeliveryFormAsync(model, ct);

        var result = await _ledger.RecordDeliveryAsync(model, UserId, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return await RebuildDeliveryFormAsync(model, ct);
        }
        TempData["Toast"] = "Delivery recorded. Bottle and money balances updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> RebuildDeliveryFormAsync(DeliveryInput model, CancellationToken ct)
    {
        var salesman = await CurrentSalesmanAsync(ct);
        if (salesman is null) return RedirectToAction("AccessDenied", "Account");
        var paid = await _settings.GetAsync("Deposit.PaidTemplate");
        var none = await _settings.GetAsync("Deposit.NoneMessage");
        var customer = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == model.CustomerId && x.AssignedSalesmanId == salesman.Id)
            .Select(x => new CustomerPickVm(x.Id, x.Code, x.User.FullName, x.BottleBalance, x.MoneyBalance,
                DepositText(x.DepositAmount, x.DepositBottles, paid, none)))
            .FirstOrDefaultAsync(ct) ?? new CustomerPickVm(model.CustomerId, "", "", 0, 0, none);
        var vm = new DeliveryFormVm
        {
            Input = model,
            Customer = customer,
            Products = await _db.Products.AsNoTracking().Where(x => x.IsActive)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.ProductId)).ToListAsync(ct),
            BottleTypes = await _db.BottleTypes.AsNoTracking().Where(x => x.IsActive)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.BottleTypeId)).ToListAsync(ct)
        };
        vm.Rate = await _rates.ResolveAsync(customer.Id, model.ProductId, model.BottleTypeId, DateTime.UtcNow, ct);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Payment(int? customerId, CancellationToken ct)
    {
        var vm = await BuildPaymentFormAsync(new PaymentInput { CustomerId = customerId ?? 0, EntryDate = DateTime.Today }, ct);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment([Bind(Prefix = "Input")] PaymentInput model, CancellationToken ct)
    {
        if (!await ApplyValidation(_paymentValidator, model)) return View(await BuildPaymentFormAsync(model, ct));
        var result = await _ledger.RecordPaymentAsync(model, UserId, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(await BuildPaymentFormAsync(model, ct));
        }
        TempData["Toast"] = "Payment received and customer balance updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<PaymentFormVm> BuildPaymentFormAsync(PaymentInput model, CancellationToken ct)
    {
        var salesman = await CurrentSalesmanAsync(ct);
        if (salesman is null) return new PaymentFormVm { Input = model };
        var customers = await _db.Customers.AsNoTracking()
            .Where(x => x.AssignedSalesmanId == salesman.Id && x.Status == AccountStatus.Active)
            .Select(x => new SelectListItem($"{x.Code} — {x.User.FullName}", x.Id.ToString(), x.Id == model.CustomerId))
            .ToListAsync(ct);
        var selected = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == model.CustomerId)
            .Select(x => new { x.BottleBalance, x.MoneyBalance }).FirstOrDefaultAsync(ct);
        return new PaymentFormVm
        {
            Input = model, Customers = customers,
            CurrentBottleBalance = selected?.BottleBalance ?? 0,
            CurrentMoneyBalance = selected?.MoneyBalance ?? 0m
        };
    }

    [HttpGet]
    public async Task<IActionResult> Expenses(int page = 1, CancellationToken ct = default)
    {
        var categories = Enum.GetValues<ExpenseCategory>()
            .Select(x => new SelectListItem(x.ToString(), ((int)x).ToString())).ToList();
        return View(new ExpensePageVm
        {
            Input = new ExpenseInput { ExpenseDate = DateTime.Today },
            Categories = categories,
            Rows = await _reports.GetExpensesAsync(null, null, page, 20, ct)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Expenses([Bind(Prefix = "Input")] ExpenseInput model, CancellationToken ct)
    {
        if (!await ApplyValidation(_expenseValidator, model))
            return View(new ExpensePageVm { Input = model, Categories = ExpenseCategories(), Rows = await _reports.GetExpensesAsync(null, null, 1, 20, ct) });

        var result = await _ledger.RecordExpenseAsync(model, UserId, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(new ExpensePageVm { Input = model, Categories = ExpenseCategories(), Rows = await _reports.GetExpensesAsync(null, null, 1, 20, ct) });
        }
        TempData["Toast"] = "Expense recorded and cash balance updated.";
        return RedirectToAction(nameof(Expenses));
    }

    private static List<SelectListItem> ExpenseCategories() => Enum.GetValues<ExpenseCategory>()
        .Select(x => new SelectListItem(x.ToString(), ((int)x).ToString())).ToList();

    [HttpGet]
    public async Task<IActionResult> Advance(CancellationToken ct)
    {
        ViewBag.Staff = await StaffOptionsAsync(null, ct);
        return View(new AdvanceInput { AdvanceDate = DateTime.Today });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Advance(AdvanceInput model, CancellationToken ct)
    {
        if (!await ApplyValidation(_advanceValidator, model))
        {
            ViewBag.Staff = await StaffOptionsAsync(model.StaffId, ct);
            return View(model);
        }
        var result = await _ledger.RecordAdvanceAsync(model, UserId, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            ViewBag.Staff = await StaffOptionsAsync(model.StaffId, ct);
            return View(model);
        }
        TempData["Toast"] = "Advance recorded and cash balance updated.";
        return RedirectToAction(nameof(Expenses));
    }

    private async Task<List<SelectListItem>> StaffOptionsAsync(int? selected, CancellationToken ct)
    {
        var items = await _db.Staff.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Code).ThenBy(x => x.FullName)
            .Select(x => new SelectListItem($"{x.Code} — {x.FullName}", x.Id.ToString(), x.Id == selected))
            .ToListAsync(ct);
        items.Insert(0, new SelectListItem("— Select staff —", "", selected is null));
        return items;
    }

    [HttpGet]
    public async Task<IActionResult> DayReport(DateTime? date, CancellationToken ct)
    {
        var day = date?.Date ?? DateTime.UtcNow.Date;
        ViewBag.Date = day;
        return View(await _reports.GetEndOfDayAsync(UserId, day, ct));
    }

    [HttpGet]
    public async Task<IActionResult> History(DateTime? from, DateTime? to, int page = 1, CancellationToken ct = default)
    {
        ViewBag.PagerRoute = new Dictionary<string, string>
        {
            ["from"] = from?.ToString("yyyy-MM-dd") ?? "",
            ["to"] = to?.ToString("yyyy-MM-dd") ?? ""
        };
        return View(new SalesmanHistoryVm
        {
            Rows = await _reports.GetAllLedgerAsync(null, (await CurrentSalesmanAsync(ct))?.Id, from, to, page, 25, ct),
            From = from, To = to, Page = page
        });
    }

    [HttpGet]
    public async Task<IActionResult> RequestChange(int entryId, CancellationToken ct)
    {
        var salesman = await CurrentSalesmanAsync(ct);
        if (salesman is null) return RedirectToAction("AccessDenied", "Account");
        var entry = await _reports.GetAllLedgerAsync(null, salesman.Id, null, null, 1, 500, ct);
        var row = entry.Items.FirstOrDefault(x => x.Id == entryId);
        if (row is null) return RedirectToAction("AccessDenied", "Account");
        return View(new ChangeRequestFormVm
        {
            Entry = row,
            Input = new ChangeRequestInput
            {
                LedgerEntryId = row.Id, RequestedFilled = row.Filled, RequestedEmpty = row.Empty,
                RequestedAmountReceived = row.Received, RequestedRate = row.Rate
            }
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestChange([Bind(Prefix = "Input")] ChangeRequestInput model, CancellationToken ct)
    {
        if (!await ApplyValidation(_requestValidator, model))
        {
            var all = await _reports.GetAllLedgerAsync(null, (await CurrentSalesmanAsync(ct))?.Id, null, null, 1, 500, ct);
            return View(new ChangeRequestFormVm { Input = model, Entry = all.Items.FirstOrDefault(x => x.Id == model.LedgerEntryId) ?? null! });
        }
        var result = await _ledger.RequestChangeAsync(model, UserId, ct);
        if (!result.Succeeded) ModelState.AddModelError(string.Empty, result.Error!);
        else TempData["Toast"] = "Request sent to the owner for approval.";
        return RedirectToAction(nameof(History));
    }
}
