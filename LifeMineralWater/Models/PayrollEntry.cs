using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class PayrollEntry
{
    public int Id { get; set; }
    [Required] public int StaffId { get; set; }
    public StaffMember Staff { get; set; } = null!;
    public DateTime Period { get; set; } // first day of month
    public decimal BasicSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetPay => BasicSalary + Allowances - Deductions;
    public PayrollStatus Status { get; set; } = PayrollStatus.Unpaid;
    public DateTime? PaidOn { get; set; }
    [Required] public int RecordedByUserId { get; set; }
}
