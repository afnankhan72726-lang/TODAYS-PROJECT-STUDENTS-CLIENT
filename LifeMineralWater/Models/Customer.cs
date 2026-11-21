using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Only users whose Role == Salesman may be assigned here (enforced in service layer).</summary>
    public int? AssignedSalesmanId { get; set; }
    public Salesman? AssignedSalesman { get; set; }

    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public int OpeningBottleBalance { get; set; }
    public int BottleBalance { get; set; }
    public decimal OpeningMoneyBalance { get; set; }
    public decimal MoneyBalance { get; set; }

    /// <summary>Bottle-deposit rule snapshot; authoritative rule lives in BottleTypes/Deposit settings.</summary>
    public decimal DepositAmount { get; set; }
    public int DepositBottles { get; set; }
    public string? ProfilePicturePath { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "System";
    public DateTime? UpdatedAt { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
}
