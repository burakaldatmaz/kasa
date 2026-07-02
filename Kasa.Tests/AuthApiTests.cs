using System.Net;
using System.Net.Http.Json;
using Kasa.Api.Contracts;

namespace Kasa.Tests;

/// <summary>
/// Faz 8 auth testleri. Her test kendi factory'sini kurar (IClassFixture değil):
/// brute force sayacı ve oturum damgası in-memory/DB durumudur, testler birbirine sızmasın.
/// Kilit süresi gerçek bekleme ile değil <see cref="KasaApiFactory.Clock"/> ile simüle edilir.
/// </summary>
public sealed class AuthApiTests : IDisposable
{
    private readonly KasaApiFactory _factory = new() { AutoLogin = false };

    public void Dispose() => _factory.Dispose();

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { password }, KasaApiFactory.Json);

    [Fact]
    public async Task WithoutSession_ApiReturns401_HealthReturns200()
    {
        var client = _factory.CreateClient();

        var transactions = await client.GetAsync("/api/transactions?date=2026-06-15");
        Assert.Equal(HttpStatusCode.Unauthorized, transactions.StatusCode);

        var categories = await client.GetAsync("/api/categories?type=Income");
        Assert.Equal(HttpStatusCode.Unauthorized, categories.StatusCode);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task UnknownApiPath_DoesNotFallToSpa_401WithoutSession_404WithSession()
    {
        var anonymous = _factory.CreateClient();
        var unauthorized = await anonymous.GetAsync("/api/olmayan-uc");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var login = await LoginAsync(anonymous, KasaApiFactory.Password);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var notFound = await anonymous.GetAsync("/api/olmayan-uc");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithTurkishMessage()
    {
        var client = _factory.CreateClient();

        var response = await LoginAsync(client, "yanlis-parola");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Equal("Parola hatalı.", error.Error);
    }

    [Fact]
    public async Task Login_CorrectPassword_SetsHardenedCookie_AndNextRequestIs200()
    {
        var client = _factory.CreateClient();

        var login = await LoginAsync(client, KasaApiFactory.Password);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        var cookie = Assert.Single(cookies, c => c.StartsWith("kasa_auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        // 30 gün kalıcı cookie: tarayıcı kapansa da oturum sürer (sliding yenileme options'ta).
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);

        var categories = await client.GetAsync("/api/categories?type=Income");
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<MeResponse>(KasaApiFactory.Json);
        Assert.NotNull(body);
        Assert.True(body.Authenticated);
    }

    [Fact]
    public async Task ProductionEnvironment_LoginCookie_IsSecure()
    {
        using var production = new KasaApiFactory { AutoLogin = false, EnvironmentName = "Production" };
        var client = production.CreateClient();

        var login = await LoginAsync(client, KasaApiFactory.Password);

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        var cookie = Assert.Single(cookies, c => c.StartsWith("kasa_auth="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BruteForce_FiveFailures_Locks15Minutes_EvenCorrectPasswordGets429()
    {
        var client = _factory.CreateClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var wrong = await LoginAsync(client, "yanlis-parola");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        // 6. istek: kilit aktif, doğru parola bile 429 alır.
        var lockedCorrect = await LoginAsync(client, KasaApiFactory.Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, lockedCorrect.StatusCode);
        var error = await lockedCorrect.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Contains("15 dakika", error.Error);

        var lockedWrong = await LoginAsync(client, "yanlis-parola");
        Assert.Equal(HttpStatusCode.TooManyRequests, lockedWrong.StatusCode);

        // 14:59 sonra hâlâ kilitli; 15 dk dolunca doğru parola girer (gerçek bekleme yok).
        _factory.Clock.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));
        var stillLocked = await LoginAsync(client, KasaApiFactory.Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, stillLocked.StatusCode);

        _factory.Clock.Advance(TimeSpan.FromSeconds(2));
        var unlocked = await LoginAsync(client, KasaApiFactory.Password);
        Assert.Equal(HttpStatusCode.NoContent, unlocked.StatusCode);

        var categories = await client.GetAsync("/api/categories?type=Income");
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
    }

    [Fact]
    public async Task Lockout_ExpiryResetsCounter_SingleFailureDoesNotRelock()
    {
        var client = _factory.CreateClient();

        for (var attempt = 1; attempt <= 5; attempt++)
            await LoginAsync(client, "yanlis-parola");
        _factory.Clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        // Kilit süresi doldu: sayaç sıfırlanır, tek hata yeni kilit başlatmaz.
        var wrong = await LoginAsync(client, "yanlis-parola");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var correct = await LoginAsync(client, KasaApiFactory.Password);
        Assert.Equal(HttpStatusCode.NoContent, correct.StatusCode);
    }

    [Fact]
    public async Task Logout_OldCookie_Returns401_EvenIfReplayedRaw()
    {
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, KasaApiFactory.Password);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        // Ham cookie değerini sakla: logout sonrası replay saldırısını temsil eder.
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        var rawCookie = Assert.Single(cookies, c => c.StartsWith("kasa_auth=")).Split(';')[0];

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Aynı istemci (cookie'si silindi) → 401.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        // Saklanan eski cookie elle gönderilse de 401: damga logout'ta döndürüldü.
        var replay = new HttpRequestMessage(HttpMethod.Get, "/api/categories?type=Income");
        replay.Headers.Add("Cookie", rawCookie);
        var replayed = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }
}
