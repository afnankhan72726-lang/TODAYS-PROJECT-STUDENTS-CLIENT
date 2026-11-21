using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class Supplier
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
