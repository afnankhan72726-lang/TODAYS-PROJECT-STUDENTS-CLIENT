namespace LifeMineralWater.Data;

public interface IAuditContext
{
    int? UserId { get; }
    string? UserName { get; }
    string CorrelationId { get; }
    string? Reason { get; }
    void Set(int? userId, string? userName, string? reason = null);
    void SetReason(string? reason);
}

/// <summary>Scoped per HTTP request. Populated by AuditContextMiddleware.</summary>
public sealed class AuditContext : IAuditContext
{
    public int? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
    public string? Reason { get; private set; }

    public void Set(int? userId, string? userName, string? reason = null)
    {
        UserId = userId;
        UserName = userName;
        Reason = reason;
    }

    public void SetReason(string? reason) => Reason = reason;
}
