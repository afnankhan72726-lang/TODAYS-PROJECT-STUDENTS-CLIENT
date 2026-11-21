using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class BankBookEntry
{
    public int Id { get; set; }
    [Required] public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    public CashDirection Direction { get; set; }
    [Range(typeof(decimal), "0.01", "10000000")] public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    [StringLength(60)] public string? Reference { get; set; }
    [StringLength(300)] public string? Description { get; set; }
    public string CreatedBy { get; set; } = "System";
    public DateTime CreatedAt { get; set; }
}
