using System.ComponentModel.DataAnnotations;
using LifeMineralWater.Common;

namespace LifeMineralWater.Models.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class UserEditVm
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [StringLength(100)] public string? Password { get; set; } // blank = keep current
    [Required] public string Role { get; set; } = AppRoles.Customer;
    [StringLength(30)] public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CustomerEditVm
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    [Required] public int UserId { get; set; }
    public int? AssignedSalesmanId { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public int OpeningBottleBalance { get; set; }
    public decimal OpeningMoneyBalance { get; set; }
    public decimal DepositAmount { get; set; }
    public int DepositBottles { get; set; }
}

public class SalesmanEditVm
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    [Required] public int UserId { get; set; }
    public decimal OpeningCash { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StaffEditVm
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(120)] public string FullName { get; set; } = string.Empty;
    [StringLength(80)] public string? Designation { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    public decimal MonthlySalary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime JoinedOn { get; set; } = DateTime.Today;
}

public class ProductEditVm
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    [StringLength(300)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BottleTypeEditVm
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    [Range(1, 100000)] public int CapacityMl { get; set; }
    [Range(typeof(decimal), "0", "100000")] public decimal DepositAmount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RateEditVm
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int? ProductId { get; set; }
    public int? BottleTypeId { get; set; }
    [Range(typeof(decimal), "0.01", "1000000")] public decimal Amount { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SupplierEditVm
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SupplierEntryVm
{
    [Required] public int SupplierId { get; set; }
    public SupplierEntryType EntryType { get; set; }
    [Range(typeof(decimal), "0.01", "10000000")] public decimal Amount { get; set; }
    public DateTime? TransactionDate { get; set; }
    [StringLength(300)] public string? Description { get; set; }
}

public class PayrollEditVm
{
    public int Id { get; set; }
    [Required] public int StaffId { get; set; }
    public DateTime Period { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    public decimal BasicSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal Deductions { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Unpaid;
}

public class AnnouncementEditVm
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required] public string Message { get; set; } = string.Empty;
    public AnnouncementType Type { get; set; }
    public DateTime StartOn { get; set; } = DateTime.Today;
    public DateTime? EndOn { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BankAccountEditVm
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string BankName { get; set; } = string.Empty;
    [Required, StringLength(120)] public string AccountTitle { get; set; } = string.Empty;
    [Required, StringLength(60)] public string AccountNumber { get; set; } = string.Empty;
    [StringLength(60)] public string? Iban { get; set; }
    [StringLength(120)] public string? Branch { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SettingEditVm
{
    public List<SystemSetting> Settings { get; set; } = new();
}

public class CashEntryVm
{
    public CashDirection Direction { get; set; }
    public CashCategory Category { get; set; }
    [Range(typeof(decimal), "0.01", "10000000")] public decimal Amount { get; set; }
    public DateTime? TransactionDate { get; set; }
    [StringLength(300)] public string? Description { get; set; }
    public int? SalesmanId { get; set; }
    public int? BankAccountId { get; set; }
}
