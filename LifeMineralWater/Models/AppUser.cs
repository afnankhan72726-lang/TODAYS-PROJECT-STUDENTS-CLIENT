using System.ComponentModel.DataAnnotations;
using LifeMineralWater.Common;

namespace LifeMineralWater.Models;

/// <summary>
/// Identity table. Portal roles are strictly Owner / Salesman / Customer.
/// Domain data (Customers, Salesmen, Staff) lives in separate tables and is never
/// rendered from this table into a business list.
/// </summary>
public class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(256), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Role { get; set; } = AppRoles.Customer;

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "System";
    public DateTime? LastLoginAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public Customer? CustomerProfile { get; set; }
    public Salesman? SalesmanProfile { get; set; }
}
