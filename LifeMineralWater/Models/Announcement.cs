using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class Announcement
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required] public string Message { get; set; } = string.Empty;
    public AnnouncementType Type { get; set; } = AnnouncementType.Announcement;
    public DateTime StartOn { get; set; }
    public DateTime? EndOn { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "System";
}
