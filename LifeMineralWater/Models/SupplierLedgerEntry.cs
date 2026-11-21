using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class SupplierLedgerEntry
{
    public int Id { get; set; }
    [Required] public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public SupplierEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime TransactionDate { get; set; }
    [StringLength(60)] public string? Reference { get; set; }
    [StringLength(300)] public string? Description { get; set; }
    public string CreatedBy { get; set; } = "System";
    public DateTime CreatedAt { get; set; }
}
