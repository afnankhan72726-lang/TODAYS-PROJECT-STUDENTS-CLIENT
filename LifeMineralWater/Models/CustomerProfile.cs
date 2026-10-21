using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LifeMineralWater.Models
{
    public class CustomerProfile
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Linked User account mapping is required")]
        public int UserId { get; set; }
        public User? User { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal StandardRate { get; set; } = 100.00m;
        [Column(TypeName = "decimal(18,2)")]
        public decimal CustomRate { get; set; }
        // --- AUTOMATED DUAL BALANCES TRACKING METRICS ---
        public int BottleBalance { get; set; } = 0;
        [Column(TypeName = "decimal(18,2)")]
        public decimal MoneyBalance { get; set; } = 0.00m;
        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositPaid { get; set; } = 0.00m;
        public int DepositBottlesCount { get; set; } = 0;
        public string? ProfilePicturePath { get; set; } // Pointer path for uploaded images
    }

}
