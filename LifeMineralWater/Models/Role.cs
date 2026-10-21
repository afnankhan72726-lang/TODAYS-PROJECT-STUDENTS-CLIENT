using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models
{
    public class Role
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Role name is required")]
        public string Name { get; set; } // Admin, Salesman, Customer
    }
}
