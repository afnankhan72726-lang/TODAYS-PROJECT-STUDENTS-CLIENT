using System.Globalization;
using System.Linq.Expressions;
using LifeMineralWater.Data;
using LifeMineralWater.Models;
using LifeMineralWater.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Services;

public sealed class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IRateService _rates;

    public InvoiceService(ApplicationDbContext db, ISettingsService settings, IRateService rates)
    {
        _db = db;
        _settings = settings;
        _rates = rates;
    }

    public async Task<MonthlyInvoiceDto> BuildAsync(int customerId, int year, int month, CancellationToken ct = default)
    {
        if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        var customer = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => new { x.Id, x.Code, x.OpeningMoneyBalance, x.DepositAmount, x.DepositBottles, x.User.FullName, x.User.Address })
            .FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Customer not found.");

        // MoneyDelta is a convenience property on the entity, not a mapped column.
        // Use a SQL-translatable expression so the aggregation runs on the server.
        var priorDelta = await _db.LedgerEntries.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.EntryDate < start)
            .SumAsync(MoneyDeltaSql, ct);
        var previous = customer.OpeningMoneyBalance + priorDelta;

        var monthRows = await _db.LedgerEntries.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.EntryDate >= start && x.EntryDate < end)
            .Select(x => new { x.FilledDelivered, x.SaleAmount, x.AmountReceived, x.AmountAdjusted, x.EntryType, x.ProductId, x.BottleTypeId, x.Rate })
            .ToListAsync(ct);

        var bottles = monthRows.Sum(x => x.FilledDelivered);
        var totalAmount = monthRows.Sum(x => x.SaleAmount);
        var payments = monthRows.Sum(x => x.AmountReceived);
        var net = monthRows.Sum(x => MoneyDelta(x.EntryType, x.SaleAmount, x.AmountReceived, x.AmountAdjusted));
        var current = previous + net;

        var rate = monthRows.FirstOrDefault(x => x.Rate > 0)?.Rate
                   ?? await _rates.ResolveAsync(customerId, monthRows.FirstOrDefault()?.ProductId,
                       monthRows.FirstOrDefault()?.BottleTypeId, start, ct);

        var dueDay = await _settings.GetIntAsync("Invoice.DueDay", 5);
        // The configured day belongs to the month AFTER the billing period.
        var dueMonth = start.AddMonths(1);
        var lastDay = DateTime.DaysInMonth(dueMonth.Year, dueMonth.Month);
        var due = new DateTime(dueMonth.Year, dueMonth.Month, Math.Clamp(dueDay, 1, lastDay));

        var paidTemplate = await _settings.GetAsync("Deposit.PaidTemplate", "Deposit Paid: Rs. {Amount} against {Bottles} Bottles");
        var noneMessage = await _settings.GetAsync("Deposit.NoneMessage", "No Deposit Paid — Bottles remain the property of the company.");
        var deposit = customer.DepositBottles > 0 || customer.DepositAmount > 0
            ? paidTemplate.Replace("{Amount}", customer.DepositAmount.ToString("N2", CultureInfo.InvariantCulture))
                          .Replace("{Bottles}", customer.DepositBottles.ToString(CultureInfo.InvariantCulture))
            : noneMessage;

        return new MonthlyInvoiceDto(
            await _settings.GetAsync("Company.Name", "Life Mineral Water"),
            await _settings.GetAsync("Company.Address"),
            await _settings.GetAsync("Company.Phone"),
            await _settings.GetAsync("Company.Email"),
            await _settings.GetAsync("Company.TaxNo"),
            customer.FullName, customer.Code, customer.Address ?? string.Empty,
            start.ToString("MMMM yyyy", CultureInfo.InvariantCulture), due,
            bottles, rate, totalAmount, previous, payments, current, deposit,
            await _settings.GetBankAccountsAsync(),
            await _settings.GetAsync("Invoice.Message"));
    }

    private static readonly Expression<Func<LedgerEntry, decimal>> MoneyDeltaSql = x =>
        (x.EntryType == LedgerEntryType.Delivery || x.EntryType == LedgerEntryType.MoneyOpening)
            ? x.SaleAmount - x.AmountReceived + x.AmountAdjusted
            : x.EntryType == LedgerEntryType.Payment
                ? -x.AmountReceived
                : x.EntryType == LedgerEntryType.Adjustment
                    ? x.AmountAdjusted - x.AmountReceived
                    : 0m;

    private static decimal MoneyDelta(LedgerEntryType type, decimal sale, decimal received, decimal adjusted) => type switch
    {
        LedgerEntryType.Delivery or LedgerEntryType.MoneyOpening => sale - received + adjusted,
        LedgerEntryType.Payment => -received,
        LedgerEntryType.Adjustment => adjusted - received,
        _ => 0m
    };

    public async Task<string> WhatsAppUrlAsync(MonthlyInvoiceDto invoice)
    {
        var template = await _settings.GetAsync("WhatsApp.Template",
            "Dear {CustomerName}, your monthly invoice for {Period} is ready. Outstanding: Rs. {Amount}. Please clear your outstanding amount by the {DueDate}. Thank you for choosing Life Mineral Water.");

        var message = template
            .Replace("{CustomerName}", invoice.CustomerName)
            .Replace("{Period}", invoice.BillingPeriod)
            .Replace("{Amount}", invoice.CurrentPayable.ToString("N2", CultureInfo.InvariantCulture))
            .Replace("{DueDate}", invoice.DueDate.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture));

        return "https://wa.me/?text=" + Uri.EscapeDataString(message);
    }
}
