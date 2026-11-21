using System.Globalization;
using LifeMineralWater.Data;
using LifeMineralWater.Models;
using LifeMineralWater.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LifeMineralWater.Services;

public sealed class SettingsService : ISettingsService
{
    private const string AllKey = "settings:all";
    private const string BanksKey = "settings:banks";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public SettingsService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        if (_cache.TryGetValue(AllKey, out Dictionary<string, string>? cached) && cached is not null)
            return cached;

        var values = await _db.SystemSettings.AsNoTracking()
            .ToDictionaryAsync(x => x.Key, x => x.Value);
        _cache.Set(AllKey, values, Ttl);
        return values;
    }

    public async Task<string> GetAsync(string key, string fallback = "")
    {
        var all = await GetAllAsync();
        return all.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    public async Task<int> GetIntAsync(string key, int fallback)
    {
        var raw = await GetAsync(key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback)
    {
        var raw = await GetAsync(key);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    public async Task<List<BankAccount>> GetBankAccountsAsync()
    {
        if (_cache.TryGetValue(BanksKey, out List<BankAccount>? cached) && cached is not null) return cached;
        var banks = await _db.BankAccounts.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id)
            .ToListAsync();
        _cache.Set(BanksKey, banks, Ttl);
        return banks;
    }

    public async Task<List<AnnouncementDto>> GetActiveAnnouncementsAsync()
    {
        var now = DateTime.UtcNow;
        return await _db.Announcements.AsNoTracking()
            .Where(x => x.IsActive && x.StartOn <= now && (x.EndOn == null || x.EndOn >= now))
            .OrderByDescending(x => x.StartOn)
            .Select(x => new AnnouncementDto(x.Id, x.Title, x.Message, x.Type, x.StartOn, x.EndOn))
            .ToListAsync();
    }

    public void Invalidate()
    {
        _cache.Remove(AllKey);
        _cache.Remove(BanksKey);
    }
}
