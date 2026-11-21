using LifeMineralWater.Common;
using LifeMineralWater.Models;
using LifeMineralWater.Models.ViewModels;

namespace LifeMineralWater.Services;

public interface ISettingsService
{
    Task<Dictionary<string, string>> GetAllAsync();
    Task<string> GetAsync(string key, string fallback = "");
    Task<int> GetIntAsync(string key, int fallback);
    Task<decimal> GetDecimalAsync(string key, decimal fallback);
    Task<List<BankAccount>> GetBankAccountsAsync();
    Task<List<AnnouncementDto>> GetActiveAnnouncementsAsync();
    void Invalidate();
}

public interface IRateService
{
    Task<decimal> ResolveAsync(int customerId, int? productId, int? bottleTypeId, DateTime onDate, CancellationToken ct = default);
}

public interface ILedgerService
{
    Task<ServiceResult> RecordDeliveryAsync(DeliveryInput model, int salesmanUserId, CancellationToken ct = default);
    Task<ServiceResult> RecordPaymentAsync(PaymentInput model, int salesmanUserId, CancellationToken ct = default);
    Task<ServiceResult> RecordExpenseAsync(ExpenseInput model, int userId, CancellationToken ct = default);
    Task<ServiceResult> RecordAdvanceAsync(AdvanceInput model, int userId, CancellationToken ct = default);
    Task<ServiceResult> AdjustAsync(AdjustmentInput model, int userId, CancellationToken ct = default);
    Task<ServiceResult> RequestChangeAsync(ChangeRequestInput model, int userId, CancellationToken ct = default);
    Task<ServiceResult> ApproveRequestAsync(int requestId, int userId, string? note, CancellationToken ct = default);
    Task<ServiceResult> RejectRequestAsync(int requestId, int userId, string? note, CancellationToken ct = default);
    Task<ServiceResult> EditEntryAsync(int entryId, DeliveryInput model, int userId, CancellationToken ct = default);
    Task<ServiceResult> DeleteEntryAsync(int entryId, int userId, CancellationToken ct = default);
    Task RecalculateCustomerAsync(int customerId, CancellationToken ct = default);
}

public interface IReportService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<PagedResult<LedgerRowDto>> GetCustomerLedgerAsync(int customerId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<LedgerRowDto>> GetAllLedgerAsync(int? customerId, int? salesmanId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<List<CustomerBalanceDto>> GetCustomerBalancesAsync(CancellationToken ct = default);
    Task<List<SalesmanCashDto>> GetSalesmanCashAsync(CancellationToken ct = default);
    Task<EndOfDayDto> GetEndOfDayAsync(int salesmanUserId, DateTime date, CancellationToken ct = default);
    Task<ProfitLossDto> GetProfitLossAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<CashBankDto> GetCashBankAsync(CancellationToken ct = default);
    Task<PagedResult<ExpenseRowDto>> GetExpensesAsync(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
    Task<List<ReceivableDto>> GetReceivablesPayablesAsync(CancellationToken ct = default);
    Task<PagedResult<SalesRowDto>> GetSalesAsync(int? customerId, int? salesmanId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default);
}

public interface IInvoiceService
{
    Task<MonthlyInvoiceDto> BuildAsync(int customerId, int year, int month, CancellationToken ct = default);
    Task<string> WhatsAppUrlAsync(MonthlyInvoiceDto invoice);
}
