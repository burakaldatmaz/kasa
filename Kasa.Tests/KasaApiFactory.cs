using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kasa.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kasa.Tests;

/// <summary>
/// In-memory SQLite üzerinde çalışan test host'u. Bağlantı factory ömrü boyunca açık tutulur
/// (kapanınca :memory: veritabanı silinir). EnsureCreated, HasData seed'lerini de uygular.
/// "Bugün" <see cref="Today"/> olarak sabitlenir — filo eksik gün sayımı deterministik test edilir.
/// Faz 8: tüm /api uçları oturum istediğinden CreateClient tek noktadan login olur
/// (<see cref="AutoLogin"/>); mevcut testler değişmeden auth'lu düzende çalışır.
/// </summary>
public sealed class KasaApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Test host'unun sabit "bugün"ü (UTC).</summary>
    public static readonly DateOnly Today = new(2026, 6, 15);

    /// <summary>Test parolası. Hash work factor 4: süiteye bcrypt maliyeti binmesin
    /// (üretim hash'i 12'dir; Verify her maliyeti okur).</summary>
    public const string Password = "test-parola";
    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>false ise CreateClient anonim döner — auth testleri login akışını kendisi sürer.</summary>
    public bool AutoLogin { get; init; } = true;

    /// <summary>Host ortamı; cookie Secure politikası gibi ortam bağımlı davranış testleri için.</summary>
    public string EnvironmentName { get; init; } = Environments.Development;

    /// <summary>Oynatılabilir test saati — brute force kilit süresi bekleme yapmadan simüle edilir.</summary>
    public MutableTimeProvider Clock { get; } = new(new DateTimeOffset(Today, new TimeOnly(12, 0), TimeSpan.Zero));

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment(EnvironmentName);
        // In-memory kaynak appsettings'ten SONRA eklenir; dev fallback hash'i her ortamda ezer.
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["PASSWORD_HASH"] = PasswordHash }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<KasaDbContext>>();
            services.AddDbContext<KasaDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    private string? _authCookie;

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        if (!AutoLogin)
            return;

        // ConfigureClient içinde istek atılamaz (BaseAddress sonra set ediliyor); login bir kez
        // TestServer istemcisiyle yapılır, cookie her istemciye varsayılan header olarak eklenir.
        // Task.Run: xUnit sync context dışında bloklanır (deadlock riski yok).
        _authCookie ??= Task.Run(LoginAndCaptureCookieAsync).GetAwaiter().GetResult();
        client.DefaultRequestHeaders.Add("Cookie", _authCookie);
    }

    private async Task<string> LoginAndCaptureCookieAsync()
    {
        using var client = Server.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { password = Password }, Json);
        if (response.StatusCode != HttpStatusCode.NoContent)
            throw new InvalidOperationException($"Test login başarısız oldu (HTTP {(int)response.StatusCode}).");

        var setCookie = response.Headers.GetValues("Set-Cookie")
            .FirstOrDefault(c => c.StartsWith("kasa_auth="))
            ?? throw new InvalidOperationException("Login yanıtında kasa_auth cookie'si yok.");
        return setCookie.Split(';')[0];
    }

    protected override IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<KasaDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}

/// <summary>Sabit başlangıçlı, elle ilerletilebilen test saati (UTC).</summary>
public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan delta) => _now += delta;
}
