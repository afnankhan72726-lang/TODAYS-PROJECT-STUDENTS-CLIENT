namespace LifeMineralWater.Common;

public sealed class ServiceResult
{
    public bool Succeeded { get; private init; }
    public string? Error { get; private init; }
    public int? EntityId { get; private init; }

    public static ServiceResult Ok(int? id = null) => new() { Succeeded = true, EntityId = id };
    public static ServiceResult Fail(string error) => new() { Succeeded = false, Error = error };
}
