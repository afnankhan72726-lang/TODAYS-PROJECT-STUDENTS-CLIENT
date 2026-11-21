using System.Security.Claims;
using LifeMineralWater.Common;
using LifeMineralWater.Data;
using LifeMineralWater.Models.ViewModels;
using LifeMineralWater.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Controllers;

[Authorize(Roles = AppRoles.Customer)]
public sealed class CustomerController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IReportService _reports;
    private readonly ISettingsService _settings;
    private readonly IInvoiceService _invoices;

    public CustomerController(ApplicationDbContext db, IReportService reports,
        ISettingsService settings, IInvoiceService invoices)
    {
        _db = db;
        _reports = reports;
        _settings = settings;
        _invoices = invoices;
    }

    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private async Task<LifeMineralWater.Models.Customer?> MyCustomerAsync(CancellationToken ct)
        => await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId, ct);

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, int page = 1, CancellationToken ct = default)
    {
        var customer = await MyCustomerAsync(ct);
        if (customer is null) return RedirectToAction("AccessDenied", "Account");

        var paid = await _settings.GetAsync("Deposit.PaidTemplate");
        var none = await _settings.GetAsync("Deposit.NoneMessage");
        var deposit = customer.DepositBottles > 0 || customer.DepositAmount > 0
            ? paid.Replace("{Amount}", customer.DepositAmount.ToString("N2"))
                  .Replace("{Bottles}", customer.DepositBottles.ToString())
            : none;

        var balances = await _reports.GetCustomerBalancesAsync(ct);
        var summary = balances.First(x => x.Id == customer.Id);
        ViewBag.CustomerName = User.Identity?.Name;
        ViewBag.PagerRoute = new Dictionary<string, string>
        {
            ["from"] = from?.ToString("yyyy-MM-dd") ?? "",
            ["to"] = to?.ToString("yyyy-MM-dd") ?? ""
        };

        return View(new CustomerPortalVm
        {
            Summary = summary,
            Ledger = await _reports.GetCustomerLedgerAsync(customer.Id, from, to, page, 25, ct),
            Announcements = await _settings.GetActiveAnnouncementsAsync(),
            DepositInfo = deposit,
            From = from, To = to, Page = page
        });
    }

    [HttpGet]
    public async Task<IActionResult> Invoice(int? year, int? month, CancellationToken ct = default)
    {
        var customer = await MyCustomerAsync(ct);
        if (customer is null) return RedirectToAction("AccessDenied", "Account");

        var y = year ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;
        if (m is < 1 or > 12) { m = DateTime.UtcNow.Month; y = DateTime.UtcNow.Year; }

        var invoice = await _invoices.BuildAsync(customer.Id, y, m, ct);
        ViewBag.Years = Enumerable.Range(y - 2, 5).Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(x.ToString(), x.ToString(), x == y)).ToList();
        ViewBag.SelectedMonth = m;
        ViewBag.WhatsAppUrl = await _invoices.WhatsAppUrlAsync(invoice);
        return View(invoice);
    }

    [HttpGet]
    public async Task<IActionResult> WhatsApp(int? year, int? month, CancellationToken ct = default)
    {
        var customer = await MyCustomerAsync(ct);
        if (customer is null) return RedirectToAction("AccessDenied", "Account");
        var invoice = await _invoices.BuildAsync(customer.Id, year ?? DateTime.UtcNow.Year, month ?? DateTime.UtcNow.Month, ct);
        return Redirect(await _invoices.WhatsAppUrlAsync(invoice));
    }
}
