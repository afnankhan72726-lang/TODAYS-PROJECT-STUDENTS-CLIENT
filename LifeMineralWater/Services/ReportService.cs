using LifeMineralWater.Common;
using LifeMineralWater.Data;
using LifeMineralWater.Models;
using LifeMineralWater.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Services;

/// <summary>Read-only, no-tracking, projected and paginated reporting queries.</summary>
public sealed class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db) => _db = db;

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var totalCustomers = await _db.Customers.AsNoTracking().CountAsync(x => x.Status == AccountStatus.Active, ct);
        var totalSalesmen = await _db.Salesmen.AsNoTracking().CountAsync(x => x.IsActive, ct);
        var receivables = await _db.Customers.AsNoTracking().Where(x => x.MoneyBalance > 0).SumAsync(x => x.MoneyBalance, ct);
        var customerAdvances = await _db.Customers.AsNoTracking().Where(x => x.MoneyBalance < 0).SumAsync(x => -x.MoneyBalance, ct);
        var supplierPayables = await _db.Suppliers.AsNoTracking().Where(x => x.CurrentBalance > 0).SumAsync(x => x.CurrentBalance, ct);
        var cashIn = await _db.CashBookEntries.AsNoTracking().SumAsync(x => x.Direction == CashDirection.In ? x.Amount : 0m, ct);
        var cashOut = await _db.CashBookEntries.AsNoTracking().SumAsync(x => x.Direction == CashDirection.Out ? x.Amount : 0m, ct);
        var bankIn = await _db.BankBookEntries.AsNoTracking().SumAsync(x => x.Direction == CashDirection.In ? x.Amount : 0m, ct);
        var bankOut = await _db.BankBookEntries.AsNoTracking().SumAsync(x => x.Direction == CashDirection.Out ? x.Amount : 0m, ct);
        var bottles = await _db.Customers.AsNoTracking().SumAsync(x => x.BottleBalance, ct);
        var todaySales = await _db.LedgerEntries.AsNoTracking()
            .Where(x => x.EntryDate >= today && x.EntryDate < tomorrow)
            .SumAsync(x => x.SaleAmount, ct);
        var pending = await _db.ModificationRequests.AsNoTracking().CountAsync(x => x.Status == RequestStatus.Pending, ct);

        return new DashboardDto(totalCustomers, totalSalesmen, receivables, customerAdvances + supplierPayables,
            cashIn - cashOut, bankIn - bankOut, bottles, todaySales, pending);
    }

    public async Task<PagedResult<LedgerRowDto>> GetCustomerLedgerAsync(int customerId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var raw = _db.LedgerEntries.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (from.HasValue) raw = raw.Where(x => x.EntryDate >= from.Value);
        if (to.HasValue) raw = raw.Where(x => x.EntryDate < to.Value.AddDays(1));

        var total = await raw.CountAsync(ct);
        var rows = await raw.OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new LedgerRowDto(
                x.Id, x.EntryDate, x.EntryType.ToString(), x.Customer.User.FullName,
                x.FilledDelivered, x.EmptyReceived, x.BottleDelta, x.Rate, x.SaleAmount,
                x.AmountReceived, x.AmountAdjusted, x.BottleBalanceAfter, x.MoneyBalanceAfter,
                x.Description, x.IsPendingModification, x.ReferenceNo))
            .ToListAsync(ct);

        return new PagedResult<LedgerRowDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<PagedResult<LedgerRowDto>> GetAllLedgerAsync(int? customerId, int? salesmanId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = _db.LedgerEntries.AsNoTracking().AsQueryable();
        if (customerId is int c) q = q.Where(x => x.CustomerId == c);
        if (salesmanId is int s) q = q.Where(x => x.SalesmanId == s);
        if (from.HasValue) q = q.Where(x => x.EntryDate >= from.Value);
        if (to.HasValue) q = q.Where(x => x.EntryDate < to.Value.AddDays(1));

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new LedgerRowDto(
                x.Id, x.EntryDate, x.EntryType.ToString(), x.Customer.User.FullName,
                x.FilledDelivered, x.EmptyReceived, x.BottleDelta, x.Rate, x.SaleAmount,
                x.AmountReceived, x.AmountAdjusted, x.BottleBalanceAfter, x.MoneyBalanceAfter,
                x.Description, x.IsPendingModification, x.ReferenceNo))
            .ToListAsync(ct);

        return new PagedResult<LedgerRowDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<List<CustomerBalanceDto>> GetCustomerBalancesAsync(CancellationToken ct = default) =>
        _db.Customers.AsNoTracking()
            .OrderBy(x => x.Code).ThenBy(x => x.User.FullName)
            .Select(x => new CustomerBalanceDto(x.Id, x.Code, x.User.FullName,
                x.AssignedSalesman == null ? "—" : x.AssignedSalesman.User.FullName,
                x.BottleBalance, x.MoneyBalance, x.DepositAmount, x.DepositBottles, x.Status.ToString()))
            .ToListAsync(ct);

    public Task<List<SalesmanCashDto>> GetSalesmanCashAsync(CancellationToken ct = default) =>
        _db.Salesmen.AsNoTracking()
            .OrderBy(x => x.User.FullName)
            .Select(x => new SalesmanCashDto(x.Id, x.User.FullName, x.OpeningCash, x.CurrentCash, x.IsActive))
            .ToListAsync(ct);

    public async Task<EndOfDayDto> GetEndOfDayAsync(int salesmanUserId, DateTime date, CancellationToken ct = default)
    {
        var salesman = await _db.Salesmen.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == salesmanUserId, ct);
        if (salesman is null) return new EndOfDayDto(0, 0, 0, 0, 0, 0, 0, 0, new List<LedgerRowDto>());

        var from = date.Date;
        var to = from.AddDays(1);
        var entriesQ = _db.LedgerEntries.AsNoTracking()
            .Where(x => x.SalesmanId == salesman.Id && x.EntryDate >= from && x.EntryDate < to);

        var served = await entriesQ.Select(x => x.CustomerId).Distinct().CountAsync(ct);
        var filled = await entriesQ.SumAsync(x => x.FilledDelivered, ct);
        var empty = await entriesQ.SumAsync(x => x.EmptyReceived, ct);
        var sales = await entriesQ.SumAsync(x => x.SaleAmount, ct);
        var collected = await entriesQ.SumAsync(x => x.AmountReceived, ct);
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(x => x.SalesmanId == salesman.Id && x.ExpenseDate >= from && x.ExpenseDate < to)
            .SumAsync(x => x.Amount, ct);
        var advances = await _db.StaffAdvances.AsNoTracking()
            .Where(x => x.SalesmanId == salesman.Id && x.AdvanceDate >= from && x.AdvanceDate < to)
            .SumAsync(x => x.Amount, ct);
        var rows = await entriesQ.OrderByDescending(x => x.EntryDate)
            .Select(x => new LedgerRowDto(x.Id, x.EntryDate, x.EntryType.ToString(), x.Customer.User.FullName,
                x.FilledDelivered, x.EmptyReceived, x.BottleDelta, x.Rate, x.SaleAmount, x.AmountReceived,
                x.AmountAdjusted, x.BottleBalanceAfter, x.MoneyBalanceAfter, x.Description,
                x.IsPendingModification, x.ReferenceNo)).Take(100).ToListAsync(ct);

        return new EndOfDayDto(served, filled, empty, sales, collected, expenses, advances,
            salesman.CurrentCash, rows);
    }

    public async Task<ProfitLossDto> GetProfitLossAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var end = to.Date.AddDays(1);
        var sales = await _db.LedgerEntries.AsNoTracking()
            .Where(x => x.EntryDate >= from.Date && x.EntryDate < end).SumAsync(x => x.SaleAmount, ct);
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDate >= from.Date && x.ExpenseDate < end && x.IsBusinessExpense)
            .SumAsync(x => x.Amount, ct);
        // NetPay is a computed, unmapped property; sum its mapped components in SQL.
        var payroll = await _db.PayrollEntries.AsNoTracking()
            .Where(x => x.PaidOn >= from.Date && x.PaidOn < end)
            .SumAsync(x => x.BasicSalary + x.Allowances - x.Deductions, ct);
        return new ProfitLossDto(sales, expenses, payroll, sales - expenses - payroll, from.Date, to.Date);
    }

    public async Task<CashBankDto> GetCashBankAsync(CancellationToken ct = default)
    {
        var cashIn = await _db.CashBookEntries.AsNoTracking()
            .Where(x => x.Direction == CashDirection.In).SumAsync(x => x.Amount, ct);
        var cashOut = await _db.CashBookEntries.AsNoTracking()
            .Where(x => x.Direction == CashDirection.Out).SumAsync(x => x.Amount, ct);
        var bankIn = await _db.BankBookEntries.AsNoTracking()
            .Where(x => x.Direction == CashDirection.In).SumAsync(x => x.Amount, ct);
        var bankOut = await _db.BankBookEntries.AsNoTracking()
            .Where(x => x.Direction == CashDirection.Out).SumAsync(x => x.Amount, ct);
        return new CashBankDto(cashIn, cashOut, cashIn - cashOut, bankIn, bankOut, bankIn - bankOut);
    }

    public async Task<PagedResult<ExpenseRowDto>> GetExpensesAsync(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = _db.Expenses.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(x => x.ExpenseDate >= from.Value);
        if (to.HasValue) q = q.Where(x => x.ExpenseDate < to.Value.AddDays(1));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.ExpenseDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ExpenseRowDto(x.Id, x.ExpenseDate, x.Category.ToString(), x.Description,
                x.Amount, x.RecordedBy.FullName, x.Salesman == null ? "—" : x.Salesman.User.FullName))
            .ToListAsync(ct);
        return new PagedResult<ExpenseRowDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<List<ReceivableDto>> GetReceivablesPayablesAsync(CancellationToken ct = default)
    {
        var customers = await _db.Customers.AsNoTracking()
            .Where(x => x.MoneyBalance != 0)
            .Select(x => new ReceivableDto(x.Id, x.User.FullName, x.BottleBalance, x.MoneyBalance,
                x.MoneyBalance > 0 ? "Receivable" : "Customer Advance"))
            .ToListAsync(ct);
        var suppliers = await _db.Suppliers.AsNoTracking()
            .Where(x => x.CurrentBalance != 0)
            .Select(x => new ReceivableDto(x.Id, x.Name, 0, x.CurrentBalance, "Supplier Payable"))
            .ToListAsync(ct);
        return customers.Concat(suppliers).OrderByDescending(x => x.MoneyBalance).ToList();
    }

    public async Task<PagedResult<SalesRowDto>> GetSalesAsync(int? customerId, int? salesmanId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = _db.LedgerEntries.AsNoTracking()
            .Where(x => x.EntryType == LedgerEntryType.Delivery).AsQueryable();
        if (customerId is int c) q = q.Where(x => x.CustomerId == c);
        if (salesmanId is int s) q = q.Where(x => x.SalesmanId == s);
        if (from.HasValue) q = q.Where(x => x.EntryDate >= from.Value);
        if (to.HasValue) q = q.Where(x => x.EntryDate < to.Value.AddDays(1));

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.EntryDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new SalesRowDto(x.Id, x.EntryDate, x.Customer.User.FullName,
                x.Salesman == null ? "—" : x.Salesman.User.FullName,
                x.FilledDelivered, x.EmptyReceived, x.Rate, x.SaleAmount, x.AmountReceived, x.MoneyBalanceAfter))
            .ToListAsync(ct);
        return new PagedResult<SalesRowDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total };
    }
}
