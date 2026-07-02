using System.Net;
using System.Net.Http.Json;
using Kasa.Api.Contracts;
using Kasa.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kasa.Tests;

/// <summary>
/// Filo endpoint'lerinin uçtan uca testleri. Fixture DB'si sınıf boyunca paylaşıldığı için
/// her test KENDİ tarihlerinde çalışır. Factory'nin sabit "bugün"ü: 2026-06-15.
/// </summary>
public class FleetApiTests(KasaApiFactory factory) : IClassFixture<KasaApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task PutFleetAsync(string date, int total, int broken, int rented)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/fleet/{date}", new { totalBikes = total, brokenBikes = broken, rentedBikes = rented });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<FleetSnapshotResponse> GetFleetAsync(string date)
    {
        var snapshot = await _client.GetFromJsonAsync<FleetSnapshotResponse>(
            $"/api/fleet/{date}", KasaApiFactory.Json);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    // ── Validasyon: ihlalde 400 + Türkçe mesaj, kayıt oluşmaz ──────────────────────────────

    [Fact]
    public async Task Put_BrokenPlusRentedExceedsTotal_Returns400()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/fleet/2026-10-01", new { totalBikes = 10, brokenBikes = 5, rentedBikes = 6 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Contains("aşamaz", error.Error);

        var get = await _client.GetAsync("/api/fleet/2026-10-01");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode); // reddedilen istek kayıt bırakmadı
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(10, -1, 0)]
    [InlineData(10, 0, -1)]
    public async Task Put_NegativeValue_Returns400(int total, int broken, int rented)
    {
        var response = await _client.PutAsJsonAsync(
            "/api/fleet/2026-10-02", new { totalBikes = total, brokenBikes = broken, rentedBikes = rented });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Contains("negatif", error.Error);
    }

    // ── Golden filo: 59 filo, 4 arızalı, 44 kirada → 44/55 = tam %80.0 ─────────────────────

    [Fact]
    public async Task PutAndGet_GoldenFleet_Returns80Point0Percent()
    {
        await PutFleetAsync("2026-10-05", 59, 4, 44);

        var snapshot = await GetFleetAsync("2026-10-05");
        Assert.Equal(new DateOnly(2026, 10, 5), snapshot.Date);
        Assert.Equal(59, snapshot.TotalBikes);
        Assert.Equal(4, snapshot.BrokenBikes);
        Assert.Equal(44, snapshot.RentedBikes);
        Assert.Equal(80.0m, snapshot.RentalPercent);
        Assert.Equal(11, snapshot.IdleBikes); // 59 − 4 − 44
        Assert.True(snapshot.BrokenAlert);
    }

    // ── Yuvarlama kenarı: 1/16 = %6.25 → AwayFromZero 6.3 (ToEven 6.2 verirdi) ─────────────

    [Fact]
    public async Task Get_RoundingEdge_UsesAwayFromZero()
    {
        await PutFleetAsync("2026-10-06", 16, 0, 1);

        var snapshot = await GetFleetAsync("2026-10-06");
        Assert.Equal(6.3m, snapshot.RentalPercent);
        Assert.False(snapshot.BrokenAlert); // arıza yok → alarm yok
    }

    // ── Tüm filo arızalı: 500 YOK, rentalPercent null ──────────────────────────────────────

    [Fact]
    public async Task Get_AllBikesBroken_RentalPercentNullNot500()
    {
        await PutFleetAsync("2026-10-08", 5, 5, 0);

        var response = await _client.GetAsync("/api/fleet/2026-10-08");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await response.Content.ReadFromJsonAsync<FleetSnapshotResponse>(KasaApiFactory.Json);
        Assert.NotNull(snapshot);
        Assert.Null(snapshot.RentalPercent);
        Assert.Equal(0, snapshot.IdleBikes);
        Assert.True(snapshot.BrokenAlert);
    }

    [Fact]
    public async Task Get_NoSnapshot_Returns404()
    {
        var response = await _client.GetAsync("/api/fleet/2026-10-30");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Upsert: aynı güne ikinci PUT kayıt sayısını artırmaz, günceller ────────────────────

    [Fact]
    public async Task Put_SameDateTwice_UpdatesSingleRecord()
    {
        await PutFleetAsync("2026-10-09", 10, 1, 2);
        await PutFleetAsync("2026-10-09", 12, 0, 6);

        var snapshot = await GetFleetAsync("2026-10-09");
        Assert.Equal(12, snapshot.TotalBikes);
        Assert.Equal(0, snapshot.BrokenBikes);
        Assert.Equal(6, snapshot.RentedBikes);
        Assert.Equal(50.0m, snapshot.RentalPercent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KasaDbContext>();
        var count = await db.FleetSnapshots.CountAsync(f => f.Date == new DateOnly(2026, 10, 9));
        Assert.Equal(1, count); // ikinci PUT yeni satır açmadı
    }

    // ── /reports/daily entegrasyonu: fleetMissing artık gerçek ─────────────────────────────

    [Fact]
    public async Task Daily_FleetMissing_TrueWithoutSnapshot_FalseWithFleetAfterPut()
    {
        var date = "2026-10-15";

        var before = await _client.GetFromJsonAsync<DailyReportResponse>(
            $"/api/reports/daily?date={date}", KasaApiFactory.Json);
        Assert.NotNull(before);
        Assert.True(before.FleetMissing);
        Assert.Null(before.Fleet);

        await PutFleetAsync(date, 59, 4, 44);

        var after = await _client.GetFromJsonAsync<DailyReportResponse>(
            $"/api/reports/daily?date={date}", KasaApiFactory.Json);
        Assert.NotNull(after);
        Assert.False(after.FleetMissing);
        Assert.NotNull(after.Fleet);
        Assert.Equal(59, after.Fleet.TotalBikes);
        Assert.Equal(4, after.Fleet.BrokenBikes);
        Assert.Equal(44, after.Fleet.RentedBikes);
        Assert.Equal(80.0m, after.Fleet.RentalPercent);
        Assert.Equal(11, after.Fleet.IdleBikes);
        Assert.True(after.Fleet.BrokenAlert);
    }

    // ── Ay özeti: geçmiş ay, 3 günlük veri (biri null yüzdeli) ─────────────────────────────

    [Fact]
    public async Task Month_ThreeDays_SummaryIsCorrect()
    {
        // Kasım 2025 tamamen geçmişte (sabit bugün: 2026-06-15) → 30 günün hepsi sayılır
        await PutFleetAsync("2025-11-01", 10, 0, 5);   // 5/10  → %50.0
        await PutFleetAsync("2025-11-02", 10, 2, 6);   // 6/8   → %75.0
        await PutFleetAsync("2025-11-03", 10, 10, 0);  // null  → ortalamaya girmez

        var report = await _client.GetFromJsonAsync<FleetMonthResponse>(
            "/api/fleet/month?month=2025-11", KasaApiFactory.Json);
        Assert.NotNull(report);

        Assert.Equal("2025-11", report.Month);
        Assert.Equal(3, report.Days.Count);
        Assert.Equal(new DateOnly(2025, 11, 1), report.Days[0].Date);
        Assert.Equal(50.0m, report.Days[0].RentalPercent);
        Assert.Equal(75.0m, report.Days[1].RentalPercent);
        Assert.Null(report.Days[2].RentalPercent);

        Assert.Equal(62.5m, report.Summary.AvgRentalPercent); // (50 + 75) / 2, null gün hariç
        Assert.Equal(12, report.Summary.TotalBrokenDays);     // 0 + 2 + 10
        Assert.Equal(27, report.Summary.MissingDays);         // 30 gün − 3 girilmiş
    }

    // ── Eksik gün sayımı: gelecek günler sayılmaz ──────────────────────────────────────────

    [Fact]
    public async Task Month_CurrentMonth_FutureDaysNotCountedAsMissing()
    {
        // Sabit bugün 2026-06-15: 1-15 Haziran sayılır (bugün dahil), 16-30 Haziran sayılmaz
        await PutFleetAsync("2026-06-01", 10, 0, 5);

        var report = await _client.GetFromJsonAsync<FleetMonthResponse>(
            "/api/fleet/month?month=2026-06", KasaApiFactory.Json);
        Assert.NotNull(report);
        Assert.Equal(14, report.Summary.MissingDays); // 15 sayılan gün − 1 girilmiş
    }

    [Fact]
    public async Task Month_FullyInFuture_HasZeroMissingDays()
    {
        var report = await _client.GetFromJsonAsync<FleetMonthResponse>(
            "/api/fleet/month?month=2026-08", KasaApiFactory.Json);
        Assert.NotNull(report);
        Assert.Empty(report.Days);
        Assert.Null(report.Summary.AvgRentalPercent);
        Assert.Equal(0, report.Summary.TotalBrokenDays);
        Assert.Equal(0, report.Summary.MissingDays);
    }

    [Theory]
    [InlineData("/api/fleet/month")]
    [InlineData("/api/fleet/month?month=2026-1")]
    [InlineData("/api/fleet/month?month=abc")]
    public async Task Month_MissingOrInvalidFormat_Returns400(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
