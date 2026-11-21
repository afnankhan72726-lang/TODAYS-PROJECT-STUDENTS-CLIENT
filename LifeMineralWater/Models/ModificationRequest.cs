using System.ComponentModel.DataAnnotations;

namespace LifeMineralWater.Models;

public class ModificationRequest
{
    public int Id { get; set; }
    /// <summary>
    /// Optional because a ledger entry can later be soft-deleted; the request must remain auditable.
    /// </summary>
    public int? LedgerEntryId { get; set; }
    public LedgerEntry? LedgerEntry { get; set; }

    /// <summary>Customer name snapshot so the request stays readable after a ledger entry is deleted.</summary>
    [StringLength(120)] public string CustomerName { get; set; } = string.Empty;

    public ModificationRequestType RequestType { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    public int RequestedFilled { get; set; }
    public int RequestedEmpty { get; set; }
    public decimal RequestedAmountReceived { get; set; }
    public decimal RequestedRate { get; set; }
    public string Reason { get; set; } = string.Empty;

    [Required] public int RequestedByUserId { get; set; }
    public AppUser RequestedBy { get; set; } = null!;
    public DateTime RequestedAt { get; set; }

    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [StringLength(500)] public string? ReviewNote { get; set; }
}
