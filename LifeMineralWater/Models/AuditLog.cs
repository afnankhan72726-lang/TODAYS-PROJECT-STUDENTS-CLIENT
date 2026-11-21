using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

/// <summary>
/// Append-only audit trail written automatically by ApplicationDbContext.SaveChangesAsync
/// in the same database transaction as the business change.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    [Required, StringLength(120)] public string EntityName { get; set; } = string.Empty;
    [Required, StringLength(60)] public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    [StringLength(1000)] public string? AffectedColumns { get; set; }
    public int? ActorUserId { get; set; }
    [StringLength(120)] public string? ActorName { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }
    [StringLength(60)] public string? CorrelationId { get; set; }
}
