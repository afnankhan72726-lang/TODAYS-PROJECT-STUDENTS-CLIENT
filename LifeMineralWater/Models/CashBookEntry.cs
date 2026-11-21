using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class CashBookEntry
{
    public int Id { get; set; }
    public int? SalesmanId { get; set; }
    public Salesman? Salesman { get; set; }
    public CashDirection Direction { get; set; }
    public CashCategory Category { get; set; }
    [Range(typeof(decimal), "0.01", "10000000")] public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    [StringLength(60)] public string? Reference { get; set; }
    [StringLength(300)] public string? Description { get; set; }
    public int? LedgerEntryId { get; set; }
    public string CreatedBy { get; set; } = "System";
    public DateTime CreatedAt { get; set; }
}
