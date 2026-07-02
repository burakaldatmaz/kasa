using System.Text.Json;
using System.Text.Json.Serialization;
using Kasa.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kasa.Tests;

/// <summary>
/// In-memory SQLite üzerinde çalışan test host'u. Bağlantı factory ömrü boyunca açık tutulur
/// (kapanınca :memory: veritabanı silinir). EnsureCreated, HasData seed'lerini de uygular.
/// "Bugün" <see cref="Today"/> olarak sabitlenir — filo eksik gün sayımı deterministik test edilir.
/// </summary>
public sealed class KasaApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Test host'unun sabit "bugün"ü (UTC).</summary>
    public static readonly DateOnly Today = new(2026, 6, 15);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<KasaDbContext>>();
            services.AddDbContext<KasaDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(
                new DateTimeOffset(Today, new TimeOnly(12, 0), TimeSpan.Zero)));
        });
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
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
