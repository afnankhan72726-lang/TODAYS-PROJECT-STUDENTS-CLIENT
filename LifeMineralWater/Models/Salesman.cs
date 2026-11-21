using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class Salesman
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public decimal OpeningCash { get; set; }
    public decimal CurrentCash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime JoinedOn { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public ICollection<Customer> AssignedCustomers { get; set; } = new List<Customer>();
}
