using System.ComponentModel.DataAnnotations;
using System.Data;

namespace LifeMineralWater.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(50)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Emailis required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password minumum 6 character")]
        public string Password { get; set; }

        [Required]
        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public string CreatedBy { get; set; } = "System";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
