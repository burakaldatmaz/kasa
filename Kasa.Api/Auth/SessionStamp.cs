using Kasa.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Kasa.Api.Auth;

/// <summary>
/// Oturum damgası: login'de cookie'ye claim olarak gömülür, her istekte mevcut damgayla
/// karşılaştırılır. Logout damgayı döndürür (rotate) — eski cookie'ler anında geçersizleşir
/// (kabul kriteri: "logout sonrası eski cookie → 401"). Damga Settings tablosunda tutulur ki
/// uygulama yeniden başlasa da geçerli oturumlar düşmesin (30 gün sliding sözü korunur).
/// </summary>
public sealed class SessionStamp(IServiceScopeFactory scopeFactory)
{
    private const string SettingKey = "AuthSessionStamp";

    private readonly SemaphoreSlim _sync = new(1, 1);
    private string? _cached;

    /// <summary>Mevcut damga; yoksa üretilip kaydedilir. Değer bellekte önbelleklenir.</summary>
    public async Task<string> GetAsync()
    {
        if (_cached is { } cached)
            return cached;

        await _sync.WaitAsync();
        try
        {
            if (_cached is { } raced)
                return raced;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KasaDbContext>();
            var setting = await db.Settings.SingleOrDefaultAsync(s => s.Key == SettingKey);
            if (setting is null)
            {
                setting = new Setting { Key = SettingKey, Value = Guid.NewGuid().ToString("N") };
                db.Settings.Add(setting);
                await db.SaveChangesAsync();
            }
            _cached = setting.Value;
            return _cached;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>Damgayı yeniler: dolaşımdaki tüm cookie'ler geçersiz olur.</summary>
    public async Task RotateAsync()
    {
        await _sync.WaitAsync();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KasaDbContext>();
            var setting = await db.Settings.SingleOrDefaultAsync(s => s.Key == SettingKey);
            if (setting is null)
            {
                setting = new Setting { Key = SettingKey, Value = Guid.NewGuid().ToString("N") };
                db.Settings.Add(setting);
            }
            else
            {
                setting.Value = Guid.NewGuid().ToString("N");
            }
            await db.SaveChangesAsync();
            _cached = setting.Value;
        }
        finally
        {
            _sync.Release();
        }
    }
}
