using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

/// <summary>Staff are employees (payroll/advances). They are NOT portal roles and never appear in customer lists.</summary>
public class StaffMember
{
    public int Id { get; set; }

    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(120)] public string FullName { get; set; } = string.Empty;
    [StringLength(80)] public string? Designation { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    public decimal MonthlySalary { get; set; }
    public bool IsActive { get; set; } = true;
    public int? UserId { get; set; }
    public DateTime JoinedOn { get; set; }
}
