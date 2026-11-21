using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

/// <summary>Bottle-type master. Deposit per bottle is configurable here (no hardcoded deposit values).</summary>
public class BottleType
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    public int CapacityMl { get; set; }
    public decimal DepositAmount { get; set; }
    public bool IsActive { get; set; } = true;
}
