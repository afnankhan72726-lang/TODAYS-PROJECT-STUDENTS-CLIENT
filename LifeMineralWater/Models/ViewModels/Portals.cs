using LifeMineralWater.Common;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LifeMineralWater.Models.ViewModels;

public record CustomerPickVm(int Id, string Code, string Name, int BottleBalance, decimal MoneyBalance, string DepositInfo);

public sealed class SalesmanDashboardVm
{
    public List<CustomerPickVm> Customers { get; set; } = new();
    public decimal OpeningCash { get; set; }
    public decimal CurrentCash { get; set; }
    public EndOfDayDto Today { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0, new List<LedgerRowDto>());
}

public sealed class DeliveryFormVm
{
    public DeliveryInput Input { get; set; } = new();
    public CustomerPickVm Customer { get; set; } = new(0, "", "", 0, 0, "");
    public decimal Rate { get; set; }
    public List<SelectListItem> Products { get; set; } = new();
    public List<SelectListItem> BottleTypes { get; set; } = new();
}

public sealed class PaymentFormVm
{
    public PaymentInput Input { get; set; } = new();
    public List<SelectListItem> Customers { get; set; } = new();
    public int CurrentBottleBalance { get; set; }
    public decimal CurrentMoneyBalance { get; set; }
}

public sealed class ExpensePageVm
{
    public ExpenseInput Input { get; set; } = new();
    public PagedResult<ExpenseRowDto> Rows { get; set; } = new();
    public List<SelectListItem> Categories { get; set; } = new();
}

public sealed class SalesmanHistoryVm
{
    public PagedResult<LedgerRowDto> Rows { get; set; } = new();
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
}

public sealed class ChangeRequestFormVm
{
    public ChangeRequestInput Input { get; set; } = new();
    public LedgerRowDto Entry { get; set; } = null!;
}

public sealed class AdminReportVm
{
    public string Title { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public string? Note { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? CustomerId { get; set; }
    public int? SalesmanId { get; set; }
    public List<SelectListItem> Customers { get; set; } = new();
    public List<SelectListItem> Salesmen { get; set; } = new();
}

public sealed class AdminFinanceVm
{
    public CashBankDto Position { get; set; } = new(0, 0, 0, 0, 0, 0);
    public List<CashBookEntry> CashEntries { get; set; } = new();
    public List<BankBookEntry> BankEntries { get; set; } = new();
    public List<BankAccount> Banks { get; set; } = new();
    public CashEntryVm Input { get; set; } = new();
    public List<SelectListItem> Salesmen { get; set; } = new();
}

public sealed class CustomerPortalVm
{
    public CustomerBalanceDto Summary { get; set; } = null!;
    public PagedResult<LedgerRowDto> Ledger { get; set; } = new();
    public string DepositInfo { get; set; } = string.Empty;
    public List<AnnouncementDto> Announcements { get; set; } = new();
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
}
