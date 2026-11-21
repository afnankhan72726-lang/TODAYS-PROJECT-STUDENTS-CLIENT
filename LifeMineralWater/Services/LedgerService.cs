using LifeMineralWater.Common;
using LifeMineralWater.Data;
using LifeMineralWater.Models;
using LifeMineralWater.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Services;

/// <summary>
/// All balance-changing operations live here. Every mutation runs inside a SQL transaction
/// and takes UPDLOCK/HOLDLOCK on the customer/salesman rows, so concurrent requests cannot drift.
/// </summary>
public sealed class LedgerService : ILedgerService
{
    private readonly ApplicationDbContext _db;
    private readonly IRateService _rates;
    private readonly IAuditContext _audit;

    public LedgerService(ApplicationDbContext db, IRateService rates, IAuditContext audit)
    {
        _db = db;
        _rates = rates;
        _audit = audit;
    }

    private Task<Customer?> LockedCustomerAsync(int id, CancellationToken ct) =>
        _db.Customers
           .FromSqlInterpolated($"SELECT * FROM Customers WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
           .OrderBy(x => x.Id)
           .FirstOrDefaultAsync(ct);

    private Task<Salesman?> LockedSalesmanAsync(int id, CancellationToken ct) =>
        _db.Salesmen
           .FromSqlInterpolated($"SELECT * FROM Salesmen WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
           .OrderBy(x => x.Id)
           .FirstOrDefaultAsync(ct);

    public async Task<ServiceResult> RecordDeliveryAsync(DeliveryInput model, int salesmanUserId, CancellationToken ct = default)
    {
        var salesman = await _db.Salesmen.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == salesmanUserId, ct);
        if (salesman is null || !salesman.IsActive) return ServiceResult.Fail("Active salesman profile not found.");

        var customer = await LockedCustomerAsync(model.CustomerId, ct);
        if (customer is null) return ServiceResult.Fail("Customer not found.");
        if (customer.Status != AccountStatus.Active) return ServiceResult.Fail("Customer is not active.");
        if (customer.AssignedSalesmanId != salesman.Id) return ServiceResult.Fail("This customer is not assigned to you.");
        if (!await _db.Products.AnyAsync(x => x.Id == model.ProductId && x.IsActive, ct)) return ServiceResult.Fail("Invalid product.");
        if (!await _db.BottleTypes.AnyAsync(x => x.Id == model.BottleTypeId && x.IsActive, ct)) return ServiceResult.Fail("Invalid bottle type.");

        var when = model.EntryDate ?? DateTime.UtcNow;
        var rate = await _rates.ResolveAsync(customer.Id, model.ProductId, model.BottleTypeId, when, ct);
        var sale = decimal.Round(model.FilledDelivered * rate, 2);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var entry = new LedgerEntry
            {
                CustomerId = customer.Id, SalesmanId = salesman.Id, EntryType = LedgerEntryType.Delivery,
                EntryDate = when, ProductId = model.ProductId, BottleTypeId = model.BottleTypeId,
                FilledDelivered = model.FilledDelivered, EmptyReceived = model.EmptyReceived,
                Rate = rate, SaleAmount = sale, AmountReceived = model.AmountReceived,
                BottleBalanceAfter = customer.BottleBalance + model.FilledDelivered - model.EmptyReceived,
                MoneyBalanceAfter = customer.MoneyBalance + sale - model.AmountReceived,
                Description = model.Description, CreatedBy = _audit.UserName ?? "Salesman", CreatedAt = DateTime.UtcNow
            };
            customer.BottleBalance += entry.BottleDelta;
            customer.MoneyBalance += entry.MoneyDelta;
            customer.UpdatedAt = DateTime.UtcNow;
            _db.LedgerEntries.Add(entry);
            await _db.SaveChangesAsync(ct);

            if (model.AmountReceived > 0)
                await CreditSalesmanCashAsync(salesman, model.AmountReceived, entry.Id, when, ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult.Ok(entry.Id);
        }
        catch (DbUpdateException ex) { return ServiceResult.Fail("Could not save delivery: " + ex.Message); }
    }

    public async Task<ServiceResult> RecordPaymentAsync(PaymentInput model, int salesmanUserId, CancellationToken ct = default)
    {
        var salesman = await _db.Salesmen.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == salesmanUserId, ct);
        if (salesman is null || !salesman.IsActive) return ServiceResult.Fail("Active salesman profile not found.");

        var customer = await LockedCustomerAsync(model.CustomerId, ct);
        if (customer is null) return ServiceResult.Fail("Customer not found.");
        if (customer.Status != AccountStatus.Active) return ServiceResult.Fail("Customer is not active.");
        if (customer.AssignedSalesmanId != salesman.Id) return ServiceResult.Fail("This customer is not assigned to you.");

        var when = model.EntryDate ?? DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var entry = new LedgerEntry
            {
                CustomerId = customer.Id, SalesmanId = salesman.Id, EntryType = LedgerEntryType.Payment,
                EntryDate = when, FilledDelivered = 0, EmptyReceived = 0,
                AmountReceived = model.AmountReceived, SaleAmount = 0, Rate = 0,
                BottleBalanceAfter = customer.BottleBalance,
                MoneyBalanceAfter = customer.MoneyBalance - model.AmountReceived,
                Description = model.Description ?? "Cash collected",
                CreatedBy = _audit.UserName ?? "Salesman", CreatedAt = DateTime.UtcNow
            };
            customer.MoneyBalance -= model.AmountReceived;
            customer.UpdatedAt = DateTime.UtcNow;
            _db.LedgerEntries.Add(entry);
            await _db.SaveChangesAsync(ct);

            await CreditSalesmanCashAsync(salesman, model.AmountReceived, entry.Id, when, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult.Ok(entry.Id);
        }
        catch (DbUpdateException ex) { return ServiceResult.Fail("Could not save payment: " + ex.Message); }
    }

    public async Task<ServiceResult> RecordExpenseAsync(ExpenseInput model, int userId, CancellationToken ct = default)
    {
        var salesman = await _db.Salesmen.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        var when = model.ExpenseDate ?? DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Lock cash first and reject cash going negative.
            Salesman? locked = null;
            if (salesman is not null)
            {
                locked = await LockedSalesmanAsync(salesman.Id, ct);
                if (locked is not null && locked.CurrentCash < model.Amount)
                    return ServiceResult.Fail("Expense exceeds cash in hand.");
            }

            var expense = new Expense
            {
                Description = model.Description, Category = model.Category, Amount = model.Amount,
                RecordedByUserId = userId, SalesmanId = salesman?.Id,
                IsBusinessExpense = model.IsBusinessExpense, ExpenseDate = when, CreatedAt = DateTime.UtcNow
            };
            _db.Expenses.Add(expense);

            _db.CashBookEntries.Add(new CashBookEntry
            {
                SalesmanId = salesman?.Id, Direction = CashDirection.Out, Category = CashCategory.Expense,
                Amount = model.Amount, TransactionDate = when, Description = model.Description,
                CreatedBy = _audit.UserName ?? "System", CreatedAt = DateTime.UtcNow
            });

            if (locked is not null) locked.CurrentCash -= model.Amount;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult.Ok(expense.Id);
        }
        catch (DbUpdateException ex) { return ServiceResult.Fail("Could not save expense: " + ex.Message); }
    }

    public async Task<ServiceResult> RecordAdvanceAsync(AdvanceInput model, int userId, CancellationToken ct = default)
    {
        if (!await _db.Staff.AnyAsync(x => x.Id == model.StaffId && x.IsActive, ct))
            return ServiceResult.Fail("Active staff member not found.");

        var salesman = await _db.Salesmen.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        var when = model.AdvanceDate ?? DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Lock cash first and reject cash going negative.
            Salesman? locked = null;
            if (salesman is not null)
            {
                locked = await LockedSalesmanAsync(salesman.Id, ct);
                if (locked is not null && locked.CurrentCash < model.Amount)
                    return ServiceResult.Fail("Advance exceeds cash in hand.");
            }

            var advance = new StaffAdvance
            {
                StaffId = model.StaffId, Amount = model.Amount, AdvanceDate = when,
                Note = model.Note, RecordedByUserId = userId, SalesmanId = salesman?.Id
            };
            _db.StaffAdvances.Add(advance);
            _db.CashBookEntries.Add(new CashBookEntry
            {
                SalesmanId = salesman?.Id, Direction = CashDirection.Out, Category = CashCategory.Advance,
                Amount = model.Amount, TransactionDate = when,
                Description = $"Advance to staff #{model.StaffId}. {model.Note}",
                CreatedBy = _audit.UserName ?? "System", CreatedAt = DateTime.UtcNow
            });
            if (locked is not null) locked.CurrentCash -= model.Amount;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult.Ok(advance.Id);
        }
        catch (DbUpdateException ex) { return ServiceResult.Fail("Could not save advance: " + ex.Message); }
    }

    public async Task<ServiceResult> AdjustAsync(AdjustmentInput model, int userId, CancellationToken ct = default)
    {
        var customer = await LockedCustomerAsync(model.CustomerId, ct);
        if (customer is null) return ServiceResult.Fail("Customer not found.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var when = DateTime.UtcNow;
            var entry = new LedgerEntry
            {
                CustomerId = customer.Id, EntryType = LedgerEntryType.Adjustment, EntryDate = when,
                BottleCorrection = model.BottleCorrection, AmountAdjusted = model.AmountAdjusted,
                AmountReceived = model.AmountReceived, Description = model.Description,
                BottleBalanceAfter = customer.BottleBalance + model.BottleCorrection,
                MoneyBalanceAfter = customer.MoneyBalance + model.AmountAdjusted - model.AmountReceived,
                CreatedBy = _audit.UserName ?? "Owner", CreatedAt = when
            };
            customer.BottleBalance += entry.BottleDelta;
            customer.MoneyBalance += entry.MoneyDelta;
            customer.UpdatedAt = when;
            _db.LedgerEntries.Add(entry);
            await _db.SaveChangesAsync(ct);

            if (model.AmountReceived > 0)
                _db.CashBookEntries.Add(new CashBookEntry
                {
                    Direction = CashDirection.In, Category = CashCategory.Collection, Amount = model.AmountReceived,
                    TransactionDate = when, Description = "Owner adjustment collection", LedgerEntryId = entry.Id,
                    CreatedBy = _audit.UserName ?? "Owner", CreatedAt = when
                });
            if (model.AmountAdjusted > 0)
                _db.CashBookEntries.Add(new CashBookEntry
                {
                    Direction = CashDirection.Out, Category = CashCategory.Other, Amount = model.AmountAdjusted,
                    TransactionDate = when, Description = "Owner adjustment paid out", LedgerEntryId = entry.Id,
                    CreatedBy = _audit.UserName ?? "Owner", CreatedAt = when
                });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult.Ok(entry.Id);
        }
        catch (DbUpdateException ex) { return ServiceResult.Fail("Could not save adjustment: " + ex.Message); }
    }

    private async Task CreditSalesmanCashAsync(Salesman salesman, decimal amount, int entryId, DateTime when, CancellationToken ct)
    {
        var locked = await LockedSalesmanAsync(salesman.Id, ct);
        if (locked is not null) locked.CurrentCash += amount;
        _db.CashBookEntries.Add(new CashBookEntry
        {
            SalesmanId = salesman.Id, Direction = CashDirection.In, Category = CashCategory.Collection,
            Amount = amount, TransactionDate = when, Description = "Customer collection",
            LedgerEntryId = entryId, CreatedBy = _audit.UserName ?? "Salesman", CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<ServiceResult> RequestChangeAsync(ChangeRequestInput model, int userId, CancellationToken ct = default)
    {
        var salesman = await _db.Salesmen.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (salesman is null) return ServiceResult.Fail("Salesman profile not found.");

        var entry = await _db.LedgerEntries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == model.LedgerEntryId, ct);
        if (entry is null) return ServiceResult.Fail("Transaction not found.");
        if (entry.SalesmanId != salesman.Id) return ServiceResult.Fail("You can only request changes to your own entries.");

        var customerName = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == entry.CustomerId)
            .Select(x => x.User.FullName)
            .FirstOrDefaultAsync(ct) ?? "Unknown customer";

        var request = new ModificationRequest
        {
            LedgerEntryId = entry.Id, CustomerName = customerName,
            RequestType = model.RequestType, Status = RequestStatus.Pending,
            RequestedFilled = model.RequestedFilled, RequestedEmpty = model.RequestedEmpty,
            RequestedAmountReceived = model.RequestedAmountReceived, RequestedRate = model.RequestedRate,
            Reason = model.Reason, RequestedByUserId = userId, RequestedAt = DateTime.UtcNow
        };
        _db.ModificationRequests.Add(request);
        entry.IsPendingModification = true;
        _db.LedgerEntries.Attach(entry);
        _db.Entry(entry).Property(x => x.IsPendingModification).IsModified = true;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok(request.Id);
    }

    public async Task<ServiceResult> ApproveRequestAsync(int requestId, int userId, string? note, CancellationToken ct = default)
    {
        var request = await _db.ModificationRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return ServiceResult.Fail("Request not found.");
        if (request.Status != RequestStatus.Pending) return ServiceResult.Fail("Request was already reviewed.");

        var entry = request.LedgerEntryId is int entryId
            ? await _db.LedgerEntries.FirstOrDefaultAsync(x => x.Id == entryId, ct)
            : null;
        if (entry is null) return ServiceResult.Fail("Linked transaction not found or deleted.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var oldReceived = entry.AmountReceived;
            if (request.RequestType == ModificationRequestType.Delete)
            {
                entry.IsDeleted = true;
                entry.DeletedBy = _audit.UserName ?? "Owner";
                entry.DeletedAt = DateTime.UtcNow;
                await SyncCashAsync(entry, oldReceived, 0, ct);
            }
            else
            {
                if (request.RequestedRate < 0) return ServiceResult.Fail("Invalid corrected rate.");
                entry.FilledDelivered = request.RequestedFilled;
                entry.EmptyReceived = request.RequestedEmpty;
                entry.Rate = request.RequestedRate;
                entry.SaleAmount = decimal.Round(request.RequestedFilled * request.RequestedRate, 2);
                entry.AmountReceived = request.RequestedAmountReceived;
                entry.ModifiedBy = _audit.UserName ?? "Owner";
                entry.ModifiedAt = DateTime.UtcNow;
                await SyncCashAsync(entry, oldReceived, entry.AmountReceived, ct);
            }
            entry.IsPendingModification = false;
            request.Status = RequestStatus.Approved;
            request.ReviewedByUserId = userId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNote = note;

            await _db.SaveChangesAsync(ct);
            await RecalculateCustomerAsync(entry.CustomerId, ct);
            await tx.CommitAsync(ct);
            return ServiceResult.Ok(entry.Id);
        }
        catch (DbUpdateException ex) { return ServiceResult.Fail("Approval failed: " + ex.Message); }
    }

    public async Task<ServiceResult> RejectRequestAsync(int requestId, int userId, string? note, CancellationToken ct = default)
    {
        var request = await _db.ModificationRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return ServiceResult.Fail("Request not found.");
        if (request.Status != RequestStatus.Pending) return ServiceResult.Fail("Request was already reviewed.");
        request.Status = RequestStatus.Rejected;
        request.ReviewedByUserId = userId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = note;

        if (request.LedgerEntryId is int entryId)
        {
            var entry = await _db.LedgerEntries.FirstOrDefaultAsync(x => x.Id == entryId, ct);
            if (entry is not null) entry.IsPendingModification = false;
        }
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok(request.Id);
    }

    public async Task<ServiceResult> EditEntryAsync(int entryId, DeliveryInput model, int userId, CancellationToken ct = default)
    {
        var entry = await _db.LedgerEntries.FirstOrDefaultAsync(x => x.Id == entryId, ct);
        if (entry is null) return ServiceResult.Fail("Transaction not found.");
        if (entry.EntryType != LedgerEntryType.Delivery) return ServiceResult.Fail("Only delivery entries can be edited here.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var oldReceived = entry.AmountReceived;
            var when = model.EntryDate ?? entry.EntryDate;
            entry.FilledDelivered = model.FilledDelivered;
            entry.EmptyReceived = model.EmptyReceived;
            entry.AmountReceived = model.AmountReceived;
            entry.EntryDate = when;
            entry.ProductId = model.ProductId;
            entry.BottleTypeId = model.BottleTypeId;
            entry.Rate = await _rates.ResolveAsync(entry.CustomerId, model.ProductId, model.BottleTypeId, when, ct);
            entry.SaleAmount = decimal.Round(model.FilledDelivered * entry.Rate, 2);
            entry.ModifiedBy = _audit.UserName ?? "Owner";
            entry.ModifiedAt = DateTime.UtcNow;
            await SyncCashAsync(entry, oldReceived, entry.AmountReceived, ct);
            await _db.SaveChangesAsync(ct);
            await RecalculateCustomerAsync(entry.CustomerId, ct);
            await tx.CommitAsync(ct);
            return ServiceResult.Ok(entry.Id);
        }
        catch (DbUpdateException ex) { return ServiceResult.Fail("Edit failed: " + ex.Message); }
    }

    public async Task<ServiceResult> DeleteEntryAsync(int entryId, int userId, CancellationToken ct = default)
    {
        var entry = await _db.LedgerEntries.FirstOrDefaultAsync(x => x.Id == entryId, ct);
        if (entry is null) return ServiceResult.Fail("Transaction not found.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await SyncCashAsync(entry, entry.AmountReceived, 0, ct);
            entry.IsDeleted = true;
            entry.DeletedBy = _audit.UserName ?? "Owner";
            entry.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await RecalculateCustomerAsync(entry.CustomerId, ct);
            await tx.CommitAsync(ct);
            return ServiceResult.Ok(entry.Id);
        }
        catch (DbUpdateException ex) { return ServiceResult.Fail("Delete failed: " + ex.Message); }
    }

    private async Task SyncCashAsync(LedgerEntry entry, decimal oldReceived, decimal newReceived, CancellationToken ct)
    {
        var delta = newReceived - oldReceived;
        if (entry.SalesmanId is int sid && delta != 0)
        {
            var salesman = await LockedSalesmanAsync(sid, ct);
            if (salesman is not null) salesman.CurrentCash += delta;
        }

        var cash = await _db.CashBookEntries.FirstOrDefaultAsync(x => x.LedgerEntryId == entry.Id, ct);
        if (newReceived <= 0)
        {
            if (cash is not null) _db.CashBookEntries.Remove(cash);
        }
        else if (cash is not null) cash.Amount = newReceived;
        else
        {
            _db.CashBookEntries.Add(new CashBookEntry
            {
                SalesmanId = entry.SalesmanId, Direction = CashDirection.In, Category = CashCategory.Collection,
                Amount = newReceived, TransactionDate = entry.EntryDate, Description = "Corrected collection",
                LedgerEntryId = entry.Id, CreatedBy = _audit.UserName ?? "Owner", CreatedAt = DateTime.UtcNow
            });
        }
    }

    public async Task RecalculateCustomerAsync(int customerId, CancellationToken ct = default)
    {
        var customer = await LockedCustomerAsync(customerId, ct);
        if (customer is null) return;

        var entries = await _db.LedgerEntries
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.EntryDate).ThenBy(x => x.Id)
            .ToListAsync(ct);

        var bottles = customer.OpeningBottleBalance;
        var money = customer.OpeningMoneyBalance;
        foreach (var e in entries)
        {
            bottles += e.BottleDelta;
            money += e.MoneyDelta;
            e.BottleBalanceAfter = bottles;
            e.MoneyBalanceAfter = money;
        }
        customer.BottleBalance = bottles;
        customer.MoneyBalance = money;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
