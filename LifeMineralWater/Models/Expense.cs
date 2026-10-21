using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LifeMineralWater.Models
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Operational cost transaction context specification is required")]
        public string Description { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        public int LoggerId { get; set; }
        public User? Logger { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
    }
}
