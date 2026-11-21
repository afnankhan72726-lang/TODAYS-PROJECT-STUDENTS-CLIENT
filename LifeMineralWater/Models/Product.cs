using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class Product
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    [StringLength(300)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
