using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Kasa.Api.Contracts;
using UglyToad.PdfPig;

namespace Kasa.Tests;

/// <summary>
/// Faz 11 rezervasyon sayaçlarının (StartedReservations / EndedReservations) uçtan uca testleri.
/// K1: sayaçlar rentalPercent hesabına girmez. K2: null = "girilmedi", 0 = "gerçekten sıfır";
/// eski istek gövdesi null yazar. K3: girilmişse >= 0, negatif 400 döner.
/// Fixture DB'si sınıf boyunca paylaşıldığı için her test KENDİ tarihlerinde çalışır.
/// </summary>
public class ReservationCounterApiTests(KasaApiFactory factory) : IClassFixture<KasaApiFactory>
{
    private const int KiralamaId = 1; // deterministik seed (KasaDbContext.SeedCategories)

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<FleetSnapshotResponse> GetFleetAsync(string date)
    {
        var snapshot = await _client.GetFromJsonAsync<FleetSnapshotResponse>(
            $"/api/fleet/{date}", KasaApiFactory.Json);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    private async Task PostIncomeAsync(string date, string amount)
    {
        var response = await _client.PostAsJsonAsync("/api/transactions",
            new { date, type = "Income", categoryId = KiralamaId, paymentMethod = "Cash", amount });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── Geriye dönük uyum: eski gövde (üç alan) 200 döner, sayaçlar null yazılır ───────────

    [Fact]
    public async Task Put_OldBodyWithoutCounters_Returns200_AndCountersAreNull()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/fleet/2026-09-01", new { totalBikes = 10, brokenBikes = 1, rentedBikes = 5 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await GetFleetAsync("2026-09-01");
        Assert.Null(snapshot.StartedReservations);
        Assert.Null(snapshot.EndedReservations);
        Assert.Equal(10, snapshot.TotalBikes); // eski davranış aynen
    }

    // ── Round-trip: PUT started=7, ended=5 → GET ve /reports/daily aynı değerleri döner ────

    [Fact]
    public async Task Put_WithCounters_RoundTripsThroughGetAndDailyReport()
    {
        var response = await _client.PutAsJsonAsync("/api/fleet/2026-09-02",
            new { totalBikes = 10, brokenBikes = 1, rentedBikes = 5, startedReservations = 7, endedReservations = 5 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await GetFleetAsync("2026-09-02");
        Assert.Equal(7, snapshot.StartedReservations);
        Assert.Equal(5, snapshot.EndedReservations);

        var daily = await _client.GetFromJsonAsync<DailyReportResponse>(
            "/api/reports/daily?date=2026-09-02", KasaApiFactory.Json);
        Assert.NotNull(daily);
        Assert.NotNull(daily.Fleet);
        Assert.Equal(7, daily.Fleet.StartedReservations);
        Assert.Equal(5, daily.Fleet.EndedReservations);
    }

    // ── K2: sayaçları güncelleyen PUT sonrası eski gövdeyle PUT sayaçları null'a döndürür ──

    [Fact]
    public async Task Put_OldBodyAfterCounters_ResetsCountersToNull()
    {
        await _client.PutAsJsonAsync("/api/fleet/2026-09-03",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5, startedReservations = 3, endedReservations = 2 });
        await _client.PutAsJsonAsync("/api/fleet/2026-09-03",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5 });

        var snapshot = await GetFleetAsync("2026-09-03");
        Assert.Null(snapshot.StartedReservations);
        Assert.Null(snapshot.EndedReservations);
    }

    // ── K3: negatif sayaç 400 + Türkçe mesaj; kayıt oluşmaz ────────────────────────────────

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task Put_NegativeCounter_Returns400WithTurkishMessage(int started, int ended)
    {
        var response = await _client.PutAsJsonAsync("/api/fleet/2026-09-04",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5, startedReservations = started, endedReservations = ended });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Contains("Rezervasyon sayıları negatif olamaz", error.Error);

        var get = await _client.GetAsync("/api/fleet/2026-09-04");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode); // reddedilen istek kayıt bırakmadı
    }

    // ── K3: sıfır geçerlidir ve null'dan ayrışır (0 = "gerçekten sıfır") ───────────────────

    [Fact]
    public async Task Put_ZeroCounters_AreStoredAsZeroNotNull()
    {
        await _client.PutAsJsonAsync("/api/fleet/2026-09-05",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5, startedReservations = 0, endedReservations = 0 });

        var snapshot = await GetFleetAsync("2026-09-05");
        Assert.Equal(0, snapshot.StartedReservations);
        Assert.Equal(0, snapshot.EndedReservations);
    }

    // ── K1: sayaçlar rentalPercent'i ETKİLEMEZ; filo adetleriyle çapraz kısıt YOK ──────────

    [Fact]
    public async Task Put_HugeCounters_DoNotAffectRentalPercent_AndHaveNoCrossConstraint()
    {
        // Golden filo (59/4/44 → %80.0) + adetlerle ilişkisiz dev sayaçlar: yine 200 + %80.0
        var response = await _client.PutAsJsonAsync("/api/fleet/2026-09-06",
            new { totalBikes = 59, brokenBikes = 4, rentedBikes = 44, startedReservations = 999, endedReservations = 999 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await GetFleetAsync("2026-09-06");
        Assert.Equal(80.0m, snapshot.RentalPercent);
        Assert.Equal(11, snapshot.IdleBikes);
        Assert.Equal(999, snapshot.StartedReservations);
    }

    // ── Ay özeti: null günler toplama katılmaz (7/5 + null/null + 3/2 → 10/7) ──────────────

    [Fact]
    public async Task Month_MixedDays_TotalsSkipNullDays()
    {
        await _client.PutAsJsonAsync("/api/fleet/2025-12-01",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5, startedReservations = 7, endedReservations = 5 });
        await _client.PutAsJsonAsync("/api/fleet/2025-12-02",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5 }); // null/null
        await _client.PutAsJsonAsync("/api/fleet/2025-12-03",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5, startedReservations = 3, endedReservations = 2 });

        var report = await _client.GetFromJsonAsync<FleetMonthResponse>(
            "/api/fleet/month?month=2025-12", KasaApiFactory.Json);
        Assert.NotNull(report);

        Assert.Equal(10, report.Summary.TotalStarted);
        Assert.Equal(7, report.Summary.TotalEnded);
        Assert.Equal(7, report.Days[0].StartedReservations); // gün satırlarına da alanlar eklendi
        Assert.Null(report.Days[1].StartedReservations);
        Assert.Null(report.Days[1].EndedReservations);
        Assert.Equal(2, report.Days[2].EndedReservations);
    }

    // ── Ay özeti: TÜM günler null ise toplamlar da null (0 değil) ──────────────────────────

    [Fact]
    public async Task Month_AllDaysNull_TotalsAreNull()
    {
        await _client.PutAsJsonAsync("/api/fleet/2025-10-01",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5 });
        await _client.PutAsJsonAsync("/api/fleet/2025-10-02",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5 });

        var report = await _client.GetFromJsonAsync<FleetMonthResponse>(
            "/api/fleet/month?month=2025-10", KasaApiFactory.Json);
        Assert.NotNull(report);
        Assert.Null(report.Summary.TotalStarted);
        Assert.Null(report.Summary.TotalEnded);
    }

    // ── PDF: değerli sayaçlar filo şeridinde görünür, tek sayfa korunur ────────────────────

    [Fact]
    public async Task Pdf_WithCounters_ShowsStartedAndEnded_SinglePage()
    {
        await PostIncomeAsync("2026-04-10", "500");
        await _client.PutAsJsonAsync("/api/fleet/2026-04-10",
            new { totalBikes = 14, brokenBikes = 4, rentedBikes = 8, startedReservations = 7, endedReservations = 5 });

        var (pages, text) = await ParsePdfAsync("2026-04-10");

        Assert.Equal(1, pages);
        Assert.Contains("Kiralama%80.0", text); // mevcut şerit içeriği bozulmadı
        Assert.Contains("Başlayan7", text);
        Assert.Contains("Biten5", text);
    }

    // ── PDF: null sayaçlar "—" olarak görünür (K2), tek sayfa korunur ──────────────────────

    [Fact]
    public async Task Pdf_NullCounters_ShowsDash_SinglePage()
    {
        await PostIncomeAsync("2026-04-15", "500");
        await _client.PutAsJsonAsync("/api/fleet/2026-04-15",
            new { totalBikes = 14, brokenBikes = 4, rentedBikes = 8 });

        var (pages, text) = await ParsePdfAsync("2026-04-15");

        Assert.Equal(1, pages);
        Assert.Contains("Başlayan—", text);
        Assert.Contains("Biten—", text);
    }

    // ── Excel round-trip: yeni sütunlar ve toplamlar JSON ile eşleşir; null → BOŞ hücre ────

    [Fact]
    public async Task Xlsx_ReservationColumns_MatchJson_AndNullIsEmptyCell()
    {
        // 2025-11: 3 işlem günü; filo: 7/5, null/null (eski gövde), 3/2
        await PostIncomeAsync("2025-11-01", "100");
        await PostIncomeAsync("2025-11-02", "100");
        await PostIncomeAsync("2025-11-03", "100");
        await _client.PutAsJsonAsync("/api/fleet/2025-11-01",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5, startedReservations = 7, endedReservations = 5 });
        await _client.PutAsJsonAsync("/api/fleet/2025-11-02",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5 });
        await _client.PutAsJsonAsync("/api/fleet/2025-11-03",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5, startedReservations = 3, endedReservations = 2 });

        var json = await _client.GetFromJsonAsync<FleetMonthResponse>(
            "/api/fleet/month?month=2025-11", KasaApiFactory.Json);
        Assert.NotNull(json);

        using var wb = await GetWorkbookAsync("2025-11");
        var ws = wb.Worksheet("Ay Özeti");

        Assert.Equal("Başlayan", ws.Cell(1, 8).GetString());
        Assert.Equal("Biten", ws.Cell(1, 9).GetString());

        // Gün satırları 2-4 (üç işlem günü): değerler JSON gün satırlarıyla birebir
        Assert.Equal(7m, ws.Cell(2, 8).GetValue<decimal>());
        Assert.Equal(5m, ws.Cell(2, 9).GetValue<decimal>());
        Assert.True(ws.Cell(3, 8).IsEmpty()); // null → boş hücre, 0 yazılmaz (K2)
        Assert.True(ws.Cell(3, 9).IsEmpty());
        Assert.Equal(3m, ws.Cell(4, 8).GetValue<decimal>());
        Assert.Equal(2m, ws.Cell(4, 9).GetValue<decimal>());

        // Filo özet satırı: toplamlar /api/fleet/month summary ile aynı (I1: server toplar)
        var f = FindLabelRow(ws, "Ortalama Kiralama %");
        Assert.Equal("Toplam Başlayan", ws.Cell(f, 7).GetString());
        Assert.Equal((decimal)json.Summary.TotalStarted!.Value, ws.Cell(f, 8).GetValue<decimal>());
        Assert.Equal("Toplam Biten", ws.Cell(f, 9).GetString());
        Assert.Equal((decimal)json.Summary.TotalEnded!.Value, ws.Cell(f, 10).GetValue<decimal>());
    }

    // ── Excel: tüm günler null ise özet toplamları "—" görünür ─────────────────────────────

    [Fact]
    public async Task Xlsx_AllCountersNull_SummaryShowsDash()
    {
        await PostIncomeAsync("2025-09-01", "100");
        await _client.PutAsJsonAsync("/api/fleet/2025-09-01",
            new { totalBikes = 10, brokenBikes = 0, rentedBikes = 5 });

        using var wb = await GetWorkbookAsync("2025-09");
        var ws = wb.Worksheet("Ay Özeti");

        Assert.True(ws.Cell(2, 8).IsEmpty());
        Assert.True(ws.Cell(2, 9).IsEmpty());

        var f = FindLabelRow(ws, "Ortalama Kiralama %");
        Assert.Equal("—", ws.Cell(f, 8).GetString());
        Assert.Equal("—", ws.Cell(f, 10).GetString());
    }

    // ── Yardımcılar (ReportPdfApiTests / ReportXlsxApiTests ile aynı desen) ────────────────

    private async Task<(int Pages, string Text)> ParsePdfAsync(string date)
    {
        var response = await _client.GetAsync($"/api/reports/daily/pdf?date={date}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        // Kelime araları font ölçümüne göre değişebildiğinden metin boşluksuz karşılaştırılır.
        var text = string.Concat(pdf.GetPages().Select(p => p.Text)).Replace(" ", string.Empty);
        return (pdf.NumberOfPages, text);
    }

    private async Task<XLWorkbook> GetWorkbookAsync(string month)
    {
        var response = await _client.GetAsync($"/api/reports/month/xlsx?month={month}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new XLWorkbook(await response.Content.ReadAsStreamAsync());
    }

    private static int FindLabelRow(IXLWorksheet ws, string label)
    {
        var last = ws.LastRowUsed()!.RowNumber();
        for (var r = 1; r <= last; r++)
            if (ws.Cell(r, 1).GetString() == label)
                return r;
        Assert.Fail($"Sheet1'de '{label}' satırı bulunamadı.");
        return -1;
    }
}
