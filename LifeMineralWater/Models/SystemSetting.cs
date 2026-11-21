using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

/// <summary>Key/value configuration. All previously hardcoded business values live here.</summary>
public class SystemSetting
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Key { get; set; } = string.Empty;
    [Required] public string Value { get; set; } = string.Empty;
    [StringLength(50)] public string Group { get; set; } = "General";
    [StringLength(150)] public string DisplayName { get; set; } = string.Empty;
    public SettingType Type { get; set; } = SettingType.Text;
    [StringLength(300)] public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "System";
}
