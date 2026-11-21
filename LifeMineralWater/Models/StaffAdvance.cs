using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class StaffAdvance
{
    public int Id { get; set; }
    [Required] public int StaffId { get; set; }
    public StaffMember Staff { get; set; } = null!;
    [Range(typeof(decimal), "0.01", "10000000")] public decimal Amount { get; set; }
    public DateTime AdvanceDate { get; set; }
    [StringLength(300)] public string? Note { get; set; }
    [Required] public int RecordedByUserId { get; set; }
    public int? SalesmanId { get; set; }
    public bool IsSettled { get; set; }
    public DateTime? SettledOn { get; set; }
}
