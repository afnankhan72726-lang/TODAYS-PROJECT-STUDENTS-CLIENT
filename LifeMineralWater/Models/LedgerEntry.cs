using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

/// <summary>
/// Immutable-by-convention financial/bottle ledger row. Balances stored are snapshots and are
/// always recalculated from all active entries after an admin correction, so they cannot drift.
/// </summary>
public class LedgerEntry
{
    public int Id { get; set; }

    [Required] public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public int? SalesmanId { get; set; }
    public Salesman? Salesman { get; set; }

    public LedgerEntryType EntryType { get; set; } = LedgerEntryType.Delivery;
    public DateTime EntryDate { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? BottleTypeId { get; set; }
    public BottleType? BottleType { get; set; }

    [Range(0, int.MaxValue)] public int FilledDelivered { get; set; }
    [Range(0, int.MaxValue)] public int EmptyReceived { get; set; }

    /// <summary>Signed correction used only for admin bottle adjustments.</summary>
    public int BottleCorrection { get; set; }

    public decimal Rate { get; set; }
    public decimal SaleAmount { get; set; }
    public decimal AmountReceived { get; set; }
    public decimal AmountAdjusted { get; set; }

    /// <summary>Previous + BottleDelta = BottleBalanceAfter.</summary>
    public int BottleBalanceAfter { get; set; }
    /// <summary>Previous + MoneyDelta = MoneyBalanceAfter.</summary>
    public decimal MoneyBalanceAfter { get; set; }

    [StringLength(300)] public string? Description { get; set; }
    [StringLength(60)] public string? ReferenceNo { get; set; }

    public bool IsDeleted { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsPendingModification { get; set; }

    public string CreatedBy { get; set; } = "System";
    public DateTime CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public int BottleDelta => EntryType switch
    {
        LedgerEntryType.Delivery => FilledDelivered - EmptyReceived,
        LedgerEntryType.BottleOpening or LedgerEntryType.Adjustment => BottleCorrection,
        _ => 0
    };

    public decimal MoneyDelta => EntryType switch
    {
        LedgerEntryType.Delivery or LedgerEntryType.MoneyOpening => SaleAmount - AmountReceived + AmountAdjusted,
        LedgerEntryType.Payment => -AmountReceived,
        LedgerEntryType.Adjustment => AmountAdjusted - AmountReceived,
        _ => 0
    };
}
