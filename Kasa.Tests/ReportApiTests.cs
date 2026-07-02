using System.Net;
using System.Net.Http.Json;
using Kasa.Api.Contracts;
using Kasa.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kasa.Tests;

/// <summary>
/// Rapor endpoint'lerinin uçtan uca testleri. Fixture DB'si sınıf boyunca paylaşıldığı için
/// her test KENDİ ayında çalışır — testler birbirinin devir zincirini etkileyemez.
/// </summary>
public class ReportApiTests(KasaApiFactory factory) : IClassFixture<KasaApiFactory>
{
    // Seed Id'leri deterministik: 1-6 gelir, 7-14 gider.
    private const int KiralamaId = 1;
    private const int EksikYakitId = 2;
    private const int EkstraServisId = 3;
    private const int ServisBakimId = 7;
    private const int YakitAlimId = 8;
    private const int KiraId = 11;
    private const int MaasId = 13;

    private readonly HttpClient _client = factory.CreateClient();

    private async Task PostTransactionAsync(
        HttpClient client, string date, string type, int categoryId, string paymentMethod, string amount)
    {
        var response = await client.PostAsJsonAsync(
            "/api/transactions", new { date, type, categoryId, paymentMethod, amount });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private Task PostTransactionAsync(string date, string type, int categoryId, string paymentMethod, string amount) =>
        PostTransactionAsync(_client, date, type, categoryId, paymentMethod, amount);

    private async Task<DailyReportResponse> GetDailyAsync(HttpClient client, string date)
    {
        var report = await client.GetFromJsonAsync<DailyReportResponse>(
            $"/api/reports/daily?date={date}", KasaApiFactory.Json);
        Assert.NotNull(report);
        return report;
    }

    private Task<DailyReportResponse> GetDailyAsync(string date) => GetDailyAsync(_client, date);

    // ── Uçtan uca golden gün: API üzerinden POST edilip /reports/daily doğrulanır ──────────

    [Fact]
    public async Task Daily_GoldenDayViaApi_ReturnsExactGoldenNumbers()
    {
        var date = "2026-07-01"; // ayın 1'i → previousBalance 0

        // Golden günün 9 satırı (GoldenDayTest.cs ile birebir aynı tutar/yöntem seti)
        await PostTransactionAsync(date, "Income", KiralamaId, "Cash", "1000");
        await PostTransactionAsync(date, "Income", KiralamaId, "Cash", "800");
        await PostTransactionAsync(date, "Income", KiralamaId, "CreditCard", "3500");
        await PostTransactionAsync(date, "Income", EkstraServisId, "Cash", "200");
        await PostTransactionAsync(date, "Income", EksikYakitId, "BankTransfer", "600");
        await PostTransactionAsync(date, "Expense", ServisBakimId, "BankTransfer", "1000");
        await PostTransactionAsync(date, "Expense", YakitAlimId, "Cash", "500");
        await PostTransactionAsync(date, "Expense", KiraId, "BankTransfer", "2300");
        await PostTransactionAsync(date, "Expense", MaasId, "Cash", "2000");

        var report = await GetDailyAsync(date);

        Assert.Equal(0, report.PreviousBalance);
        Assert.Equal(610000, report.IncomeTotal);
        Assert.Equal(580000, report.ExpenseTotal);
        Assert.Equal(12250, report.PosFee);
        Assert.Equal(17750, report.DayNet);
        Assert.Equal(17750, report.ClosingBalance);
        Assert.True(report.FleetMissing);

        // Satırlar CreatedAt sırasıyla ve tam kadro döner
        Assert.Equal(5, report.IncomeLines.Count);
        Assert.Equal(4, report.ExpenseLines.Count);
        Assert.Equal(
            [100000L, 80000L, 350000L, 20000L, 60000L],
            report.IncomeLines.Select(l => l.AmountSatang).ToArray());

        // Kategori toplamları: sadece o gün kullanılan kategoriler
        Assert.Equal(3, report.IncomeByCategory.Count);
        Assert.Equal(530000, report.IncomeByCategory.Single(c => c.Category == "Kiralama").TotalSatang);
        Assert.Equal(20000, report.IncomeByCategory.Single(c => c.Category == "Ekstra Servis").TotalSatang);
        Assert.Equal(60000, report.IncomeByCategory.Single(c => c.Category == "Eksik Yakıt Tahsilatı").TotalSatang);
        Assert.Equal(4, report.ExpenseByCategory.Count);
        Assert.Equal(230000, report.ExpenseByCategory.Single(c => c.Category == "Kira").TotalSatang);
    }

    // ── 3 günlük devir zinciri: gün 2 eksiye düşer, gün 3 devri doğru taşır ────────────────

    [Fact]
    public async Task Daily_ThreeDayChain_CarriesNegativeBalance()
    {
        await PostTransactionAsync("2026-03-10", "Income", KiralamaId, "Cash", "100");   // net +10000
        await PostTransactionAsync("2026-03-11", "Expense", ServisBakimId, "Cash", "250"); // net -25000
        await PostTransactionAsync("2026-03-12", "Income", KiralamaId, "Cash", "50");    // net +5000

        var day1 = await GetDailyAsync("2026-03-10");
        Assert.Equal(0, day1.PreviousBalance);
        Assert.Equal(10000, day1.ClosingBalance);

        var day2 = await GetDailyAsync("2026-03-11");
        Assert.Equal(10000, day2.PreviousBalance);
        Assert.Equal(-15000, day2.ClosingBalance); // eksiye düştü

        var day3 = await GetDailyAsync("2026-03-12");
        Assert.Equal(-15000, day3.PreviousBalance); // eksi devir aynen taşındı
        Assert.Equal(-10000, day3.ClosingBalance);
    }

    // ── Ay sınırı: önceki ayın bakiyesi ne olursa olsun yeni ayın 1'inde devir 0 ───────────

    [Fact]
    public async Task Daily_FirstOfMonth_PreviousBalanceIsZero_RegardlessOfPriorMonth()
    {
        await PostTransactionAsync("2026-05-31", "Income", KiralamaId, "Cash", "700");

        var lastDayOfMay = await GetDailyAsync("2026-05-31");
        Assert.Equal(70000, lastDayOfMay.ClosingBalance);

        var firstOfJune = await GetDailyAsync("2026-06-01");
        Assert.Equal(0, firstOfJune.PreviousBalance);
        Assert.Equal(0, firstOfJune.ClosingBalance);
        Assert.Empty(firstOfJune.IncomeLines);
        Assert.Empty(firstOfJune.ExpenseLines);
    }

    // ── Geçmişe düzeltme: gün 1'e sonradan işlem eklenince gün 5'in deviri değişir ─────────

    [Fact]
    public async Task Daily_RetroactiveEntry_UpdatesLaterPreviousBalance()
    {
        await PostTransactionAsync("2026-04-05", "Income", KiralamaId, "Cash", "300");

        var before = await GetDailyAsync("2026-04-05");
        Assert.Equal(0, before.PreviousBalance);

        // Geçmişe düzeltme: ayın 1'ine sonradan gelir girilir
        await PostTransactionAsync("2026-04-01", "Income", KiralamaId, "Cash", "1000");

        var after = await GetDailyAsync("2026-04-05");
        Assert.Equal(100000, after.PreviousBalance); // devir saklanmıyor, yeniden hesaplandı
        Assert.Equal(130000, after.ClosingBalance);
    }

    // ── İşlemsiz gün 200 döner; ay ortasında boş gün devri aynen taşır ─────────────────────

    [Fact]
    public async Task Daily_EmptyDayMidMonth_Returns200AndCarriesBalance()
    {
        await PostTransactionAsync("2026-02-10", "Income", KiralamaId, "Cash", "400");

        var response = await _client.GetAsync("/api/reports/daily?date=2026-02-12");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var emptyDay = await response.Content.ReadFromJsonAsync<DailyReportResponse>(KasaApiFactory.Json);
        Assert.NotNull(emptyDay);
        Assert.Empty(emptyDay.IncomeLines);
        Assert.Empty(emptyDay.ExpenseLines);
        Assert.Empty(emptyDay.IncomeByCategory);
        Assert.Equal(40000, emptyDay.PreviousBalance); // 10 Şubat'ın kapanışı, 11 Şubat boş
        Assert.Equal(0, emptyDay.DayNet);
        Assert.Equal(40000, emptyDay.ClosingBalance);
    }

    [Fact]
    public async Task Daily_WithoutDate_Returns400()
    {
        var response = await _client.GetAsync("/api/reports/daily");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── PosFeeRate Settings'ten okunur: izole DB'de 0.05 yapılınca posFee değişir ──────────

    [Fact]
    public async Task Daily_PosFee_UsesRateFromSettings()
    {
        // İzole DB: paylaşılan fixture'ın 0.035 oranı bozulmaz
        using var isolatedFactory = new KasaApiFactory();
        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KasaDbContext>();
            var setting = await db.Settings.SingleAsync(s => s.Key == "PosFeeRate");
            setting.Value = "0.05";
            await db.SaveChangesAsync();
        }

        var client = isolatedFactory.CreateClient();
        await PostTransactionAsync(client, "2026-09-01", "Income", KiralamaId, "CreditCard", "1000");

        var report = await GetDailyAsync(client, "2026-09-01");
        Assert.Equal(5000, report.PosFee); // 100000 × 0.05; 0.035 olsaydı 3500 çıkardı
        Assert.Equal(95000, report.DayNet);
    }

    // ── Ay raporu: boş günler atlanır, kümülatif akar, dağıtım Settings'ten ────────────────

    [Fact]
    public async Task Month_ChainTotalsAndDistribution_AreCorrect()
    {
        // 5 Ocak: 1000 gelir − 200 gider = net 80000
        await PostTransactionAsync("2026-01-05", "Income", KiralamaId, "Cash", "1000");
        await PostTransactionAsync("2026-01-05", "Expense", ServisBakimId, "Cash", "200");
        // 6-7 Ocak boş → days'te yer almaz. 8 Ocak: 500 kredi kartı → posFee 1750, net 48250
        await PostTransactionAsync("2026-01-08", "Income", EkstraServisId, "CreditCard", "500");

        var report = await _client.GetFromJsonAsync<MonthReportResponse>(
            "/api/reports/month?month=2026-01", KasaApiFactory.Json);
        Assert.NotNull(report);

        Assert.Equal("2026-01", report.Month);
        Assert.Equal(2, report.Days.Count); // boş günler atlandı

        Assert.Equal(new DateOnly(2026, 1, 5), report.Days[0].Date);
        Assert.Equal(80000, report.Days[0].DayNet);
        Assert.Equal(80000, report.Days[0].CumulativeBalance);

        Assert.Equal(new DateOnly(2026, 1, 8), report.Days[1].Date);
        Assert.Equal(1750, report.Days[1].PosFee);
        Assert.Equal(48250, report.Days[1].DayNet);
        Assert.Equal(128250, report.Days[1].CumulativeBalance); // kümülatif boş günlerden doğru aktı

        Assert.Equal(128250, report.FinalBalance);

        // Faz 7: alt toplam satırı alanları (geriye dönük uyumlu DTO eki — istemci toplamaz, I1)
        Assert.Equal(150000, report.Totals.IncomeTotal);
        Assert.Equal(20000, report.Totals.ExpenseTotal);
        Assert.Equal(1750, report.Totals.PosFee);
        Assert.Equal(report.FinalBalance, report.Totals.DayNet);

        Assert.Equal(100000, report.IncomeByCategory.Single(c => c.Category == "Kiralama").TotalSatang);
        Assert.Equal(50000, report.IncomeByCategory.Single(c => c.Category == "Ekstra Servis").TotalSatang);
        Assert.Equal(20000, report.ExpenseByCategory.Single(c => c.Category == "Servis Bakım").TotalSatang);

        // Dağıtım: partner1 = 128250 × 0.90 = 115425, partner2 = kalan; toplam = finalBalance
        Assert.Equal("Amornrat Thanmaen", report.Distribution.Partner1.Name);
        Assert.Equal(90m, report.Distribution.Partner1.SharePercent);
        Assert.Equal(115425, report.Distribution.Partner1.AmountSatang);
        Assert.Equal("Thanchanok Sabancıoğlu", report.Distribution.Partner2.Name);
        Assert.Equal(10m, report.Distribution.Partner2.SharePercent);
        Assert.Equal(12825, report.Distribution.Partner2.AmountSatang);
        Assert.Equal(report.FinalBalance,
            report.Distribution.Partner1.AmountSatang + report.Distribution.Partner2.AmountSatang);
    }

    [Theory]
    [InlineData("/api/reports/month")]
    [InlineData("/api/reports/month?month=2026")]
    [InlineData("/api/reports/month?month=2026-1")]
    [InlineData("/api/reports/month?month=abc")]
    public async Task Month_MissingOrInvalidFormat_Returns400(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
