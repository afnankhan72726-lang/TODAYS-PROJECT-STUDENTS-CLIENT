namespace LifeMineralWater.Models.ViewModels;

public record AnnouncementDto(int Id, string Title, string Message, AnnouncementType Type, DateTime StartOn, DateTime? EndOn);

public record LedgerRowDto(
    int Id, DateTime EntryDate, string EntryType, string CustomerName,
    int Filled, int Empty, int BottleDelta, decimal Rate, decimal SaleAmount,
    decimal Received, decimal Adjusted, int BottleBalanceAfter, decimal MoneyBalanceAfter,
    string? Description, bool IsPending, string? ReferenceNo);

public record CustomerBalanceDto(
    int Id, string Code, string Name, string Salesman, int BottleBalance,
    decimal MoneyBalance, decimal DepositAmount, int DepositBottles, string Status);

public record SalesmanCashDto(int Id, string Name, decimal OpeningCash, decimal CurrentCash, bool IsActive);

public record EndOfDayDto(
    int CustomersServed, int FilledDelivered, int EmptyReceived, decimal Sales,
    decimal CashCollected, decimal Expenses, decimal Advances, decimal ClosingCash,
    List<LedgerRowDto> Entries);

public record ProfitLossDto(decimal Sales, decimal Expenses, decimal Payroll, decimal Profit, DateTime From, DateTime To);

public record CashBankDto(decimal CashIn, decimal CashOut, decimal CashPosition, decimal BankIn, decimal BankOut, decimal BankPosition);

public record ExpenseRowDto(int Id, DateTime Date, string Category, string Description, decimal Amount, string RecordedBy, string? Salesman);

public record ReceivableDto(int Id, string Name, int BottleBalance, decimal MoneyBalance, string Type);

public record SalesRowDto(
    int Id, DateTime Date, string Customer, string Salesman, int Filled, int Empty,
    decimal Rate, decimal SaleAmount, decimal Received, decimal BalanceAfter);

public record DashboardDto(
    int TotalCustomers, int TotalSalesmen, decimal Receivables, decimal Payables,
    decimal CashPosition, decimal BankPosition, int BottlesInCirculation,
    decimal TodaySales, int PendingRequests);

public record MonthlyInvoiceDto(
    string CompanyName, string CompanyAddress, string CompanyPhone, string CompanyEmail, string TaxNo,
    string CustomerName, string CustomerCode, string CustomerAddress, string BillingPeriod,
    DateTime DueDate, int TotalBottlesSupplied, decimal Rate, decimal TotalAmount,
    decimal PreviousBalance, decimal PaymentsReceived, decimal CurrentPayable,
    string DepositInfo, List<BankAccount> Banks, string Message);
