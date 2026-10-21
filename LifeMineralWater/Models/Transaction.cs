using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LifeMineralWater.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public CustomerProfile? Customer { get; set; }

        [Required]
        public int SalesmanId { get; set; }
        public User? Salesman { get; set; }

        public int FilledDelivered { get; set; }
        public int EmptyReceived { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalBill { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        public string CreatedBy { get; set; } = "System";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsPendingModification { get; set; } = false;
        public string? ModificationReason { get; set; }
    }
}
