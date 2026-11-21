using LifeMineralWater.Data;
using Microsoft.EntityFrameworkCore;

namespace LifeMineralWater.Services;

/// <summary>
/// Resolves the applicable rate on the server. Client supplied rates are NEVER accepted.
/// Priority: customer-specific + product + bottle &gt; customer + bottle &gt; standard + product + bottle &gt; default.
/// </summary>
public sealed class RateService : IRateService
{
    private readonly ApplicationDbContext _db;
    private readonly ISettingsService _settings;

    public RateService(ApplicationDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<decimal> ResolveAsync(int customerId, int? productId, int? bottleTypeId, DateTime onDate, CancellationToken ct = default)
    {
        var rate = await _db.Rates.AsNoTracking()
            .Where(r => r.IsActive && r.EffectiveFrom <= onDate && (r.EffectiveTo == null || r.EffectiveTo >= onDate))
            .OrderByDescending(r => r.CustomerId == customerId ? 2 : r.CustomerId == null ? 1 : 0)
            .ThenByDescending(r => r.BottleTypeId == bottleTypeId ? 2 : r.BottleTypeId == null ? 1 : 0)
            .ThenByDescending(r => r.ProductId == productId ? 2 : r.ProductId == null ? 1 : 0)
            .ThenByDescending(r => r.EffectiveFrom)
            .Select(r => (decimal?)r.Amount)
            .FirstOrDefaultAsync(ct);

        return rate ?? await _settings.GetDecimalAsync("Rates.Default", 100m);
    }
}
