using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

/// <summary>
/// Rate history. CustomerId == null means a standard rate.
/// EffectiveFrom/To make every rate change auditable and time-correct.
/// </summary>
public class Rate
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? BottleTypeId { get; set; }
    public BottleType? BottleType { get; set; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Amount { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "System";
}
