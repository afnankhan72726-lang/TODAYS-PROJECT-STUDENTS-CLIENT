using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class BankAccount
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
