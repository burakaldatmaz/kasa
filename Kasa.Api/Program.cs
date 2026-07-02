using System.Text.Json.Serialization;
using Kasa.Api.Auth;
using Kasa.Api.Contracts;
using Kasa.Api.Data;
using Kasa.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

// Ops aracı: PASSWORD_HASH için bcrypt hash üretir (work factor 12).
// Kullanım: dotnet Kasa.Api.dll hash-password '<parola>'  (compose: docker compose run --rm api hash-password '<parola>')
if (args is ["hash-password", ..])
{
    var password = args.Length > 1 ? args[1] : Console.ReadLine();
    Console.WriteLine(string.IsNullOrEmpty(password)
        ? "Kullanım: hash-password <parola>"
        : BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12));
    return;
}

// Ops aracı: uygulamanın gördüğü saat/dilim (TZ doğrulaması; "bugün" GetLocalNow'dan türer).
if (args is ["now"])
{
    var clock = TimeProvider.System;
    Console.WriteLine($"utc   = {clock.GetUtcNow():O}");
    Console.WriteLine($"local = {clock.GetLocalNow():O}");
    Console.WriteLine($"zone  = {TimeZoneInfo.Local.Id}");
    Console.WriteLine($"today = {DateOnly.FromDateTime(clock.GetLocalNow().DateTime):yyyy-MM-dd}");
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<KasaDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Kasa") ?? "Data Source=kasa.db"));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// "Bugün" kavramı (filo eksik gün sayımı) testte sabitlenebilsin diye DI üzerinden verilir.
builder.Services.AddSingleton(TimeProvider.System);

// Tek kullanıcı, cookie tabanlı oturum. Parola hash'i config'ten (PASSWORD_HASH ortam
// değişkeni; dev fallback appsettings.Development.json).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "kasa_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        // Production'da HTTPS zorunlu (nginx arkasında); dev/test http üzerinden de çalışabilsin.
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.EventsType = typeof(KasaCookieEvents);
    });

builder.Services.AddSingleton<SessionStamp>();
builder.Services.AddSingleton<KasaCookieEvents>();
builder.Services.AddSingleton<LoginRateLimiter>();

// Varsayılan: TÜM endpoint'ler oturum ister. İstisnalar AllowAnonymous ile tek tek işaretli
// (/api/auth/login, /health, SPA fallback). Yeni endpoint'ler otomatik korunur.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

// Production'da parola hash'i olmadan açılmayı reddet: sessizce login'siz kalmaktansa
// compose up anında net hata ver (hash üretimi: dotnet Kasa.Api.dll hash-password).
if (app.Environment.IsProduction() && string.IsNullOrEmpty(app.Configuration["PASSWORD_HASH"]))
    throw new InvalidOperationException("PASSWORD_HASH ortam değişkeni tanımlı değil (bkz. .env.example).");

// Docker'da sıfır kurulum: /data volume'ü boşsa migration'lar şemayı ve seed'i oluşturur.
if (app.Configuration.GetValue<bool>("AUTO_MIGRATE"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<KasaDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

// Frontend build çıktısı (wwwroot) API ile aynı origin'den servis edilir (Dockerfile stage 3).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCategoryEndpoints();
app.MapTransactionEndpoints();
app.MapReportEndpoints();
app.MapFleetEndpoints();

// İzleme ucu: auth istemez, DB'ye gerçek bir sorgu atar.
app.MapGet("/health", async (KasaDbContext db) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.Json(new { status = "db-hatasi" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

// Bilinmeyen /api path'leri SPA fallback'e düşmez: auth'suz 401, auth'lu 404 alır.
app.MapFallback("/api/{**path}", () => Results.NotFound(new ErrorResponse("Uç bulunamadı.")));

// SPA fallback: /rapor, /ay, /login gibi derin linkler yenilemede index.html'e düşer.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

// WebApplicationFactory<Program> erişimi için (Kasa.Tests).
public partial class Program { }
