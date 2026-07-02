using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Kasa.Api.Contracts;

namespace Kasa.Tests;

/// <summary>
/// /api/reports/month/xlsx uçtan uca testleri. Üretilen dosya ClosedXML ile GERİ AÇILIR
/// ve hücre değerleri JSON ay raporuyla karşılaştırılır — hücreler SAYI değerdir, formül
/// içermez (I1); dolayısıyla okunan değer Excel'in göstereceği değerin ta kendisidir.
/// Her test kendi ayında çalışır; devir zincirleri birbirine karışmaz.
/// </summary>
public class ReportXlsxApiTests(KasaApiFactory factory) : IClassFixture<KasaApiFactory>
{
    // Seed Id'leri deterministik: 1-6 gelir, 7-14 gider (KasaDbContext.SeedCategories).
    private const int KiralamaId = 1;
    private const int EksikYakitId = 2;
    private const int KiralamaUzatmaId = 4;
    private const int ServisBakimId = 7;
    private const int YakitAlimId = 8;
    private const int KiraId = 11;
    private const int MaasId = 13;

    private readonly HttpClient _client = factory.CreateClient();

    private async Task PostTransactionAsync(
        string date, string type, int categoryId, string paymentMethod, string amount, string? note = null)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/transactions", new { date, type, categoryId, paymentMethod, amount, note });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<XLWorkbook> GetWorkbookAsync(string month)
    {
        var response = await _client.GetAsync($"/api/reports/month/xlsx?month={month}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stream = await response.Content.ReadAsStreamAsync();
        return new XLWorkbook(stream);
    }

    private async Task<MonthReportResponse> GetMonthJsonAsync(string month)
    {
        var report = await _client.GetFromJsonAsync<MonthReportResponse>(
            $"/api/reports/month?month={month}", KasaApiFactory.Json);
        Assert.NotNull(report);
        return report;
    }

    /// <summary>Sheet1'de tarihe göre gün satırı arar (satır 2'den TOPLAM'a kadar).</summary>
    private static int FindDayRow(IXLWorksheet ws, DateOnly date)
    {
        var last = ws.LastRowUsed()!.RowNumber();
        for (var r = 2; r <= last; r++)
        {
            var cell = ws.Cell(r, 1);
            if (cell.DataType == XLDataType.DateTime && cell.GetDateTime() == date.ToDateTime(TimeOnly.MinValue))
                return r;
        }
        Assert.Fail($"Sheet1'de {date:yyyy-MM-dd} satırı bulunamadı.");
        return -1;
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

    private static decimal Baht(long satang) => satang / 100m;

    // ── Sözleşme: 200 + doğru content-type + dosya adı ─────────────────────────────────────

    [Fact]
    public async Task Xlsx_Returns200_WithXlsxContentTypeAndFileName()
    {
        var response = await _client.GetAsync("/api/reports/month/xlsx?month=2026-12");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("kasa-2026-12.xlsx", response.Content.Headers.ContentDisposition?.FileName);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    // ── Golden ay: Sheet1/2/3 değerleri JSON ay raporuyla birebir aynı ─────────────────────

    [Fact]
    public async Task Xlsx_GoldenMonth_MatchesJsonReport()
    {
        var month = "2026-08";
        var goldenDate = new DateOnly(2026, 8, 3); // kasa.db'deki golden günün birebir kopyası

        await PostTransactionAsync("2026-08-03", "Income", KiralamaId, "Cash", "1000", "Honda Click 3 gün");
        await PostTransactionAsync("2026-08-03", "Income", KiralamaId, "Cash", "800");
        await PostTransactionAsync("2026-08-03", "Income", KiralamaId, "CreditCard", "3500", "Haftalık kiralama");
        await PostTransactionAsync("2026-08-03", "Income", EksikYakitId, "Cash", "200");
        await PostTransactionAsync("2026-08-03", "Income", KiralamaUzatmaId, "BankTransfer", "600");
        await PostTransactionAsync("2026-08-03", "Expense", KiraId, "BankTransfer", "1000", "Dükkan kirası taksit");
        await PostTransactionAsync("2026-08-03", "Expense", YakitAlimId, "Cash", "500");
        await PostTransactionAsync("2026-08-03", "Expense", ServisBakimId, "BankTransfer", "2300", "Fren balata + yağ");
        await PostTransactionAsync("2026-08-03", "Expense", MaasId, "Cash", "2000");
        // İkinci gün: kümülatifin aktığını kanıtlar (5 Ağustos, filo snapshot'sız → Kiralama % "—")
        await PostTransactionAsync("2026-08-05", "Income", KiralamaId, "Cash", "1000");

        var fleet = await _client.PutAsJsonAsync("/api/fleet/2026-08-03",
            new { totalBikes = 14, brokenBikes = 4, rentedBikes = 8 });
        Assert.Equal(HttpStatusCode.OK, fleet.StatusCode);

        var json = await GetMonthJsonAsync(month);
        using var wb = await GetWorkbookAsync(month);
        var ws = wb.Worksheet("Ay Özeti");

        // Golden gün satırı: Gün Net 177.50, kümülatif 177.50, Kiralama %80.0
        var golden = FindDayRow(ws, goldenDate);
        Assert.Equal(6100.00m, ws.Cell(golden, 2).GetValue<decimal>()); // gelir toplamı
        Assert.Equal(177.50m, ws.Cell(golden, 5).GetValue<decimal>());
        Assert.Equal(177.50m, ws.Cell(golden, 6).GetValue<decimal>());
        Assert.Equal(80.0m, ws.Cell(golden, 7).GetValue<decimal>());
        Assert.Equal("#,##0.00 ฿", ws.Cell(golden, 5).Style.NumberFormat.Format); // BKKBIKE para formatı

        // 5 Ağustos: kümülatif 177.50 + 1000.00 = 1177.50; snapshot yok → "—"
        var second = FindDayRow(ws, new DateOnly(2026, 8, 5));
        Assert.Equal(1000.00m, ws.Cell(second, 5).GetValue<decimal>());
        Assert.Equal(1177.50m, ws.Cell(second, 6).GetValue<decimal>());
        Assert.Equal("—", ws.Cell(second, 7).GetString());

        // Toplam satırı == /api/reports/month JSON toplamları
        var total = FindLabelRow(ws, "TOPLAM");
        Assert.Equal(Baht(json.Totals.IncomeTotal), ws.Cell(total, 2).GetValue<decimal>());
        Assert.Equal(Baht(json.Totals.ExpenseTotal), ws.Cell(total, 3).GetValue<decimal>());
        Assert.Equal(Baht(json.Totals.PosFee), ws.Cell(total, 4).GetValue<decimal>());
        Assert.Equal(Baht(json.Totals.DayNet), ws.Cell(total, 5).GetValue<decimal>());
        Assert.Equal(Baht(json.FinalBalance), ws.Cell(total, 6).GetValue<decimal>());

        // Dağıtım bloğu: isim+yüzde DTO'dan, partner1 + partner2 == finalBalance (baht)
        var ana = FindLabelRow(ws, "Ay Sonu Ana Kasa");
        Assert.Equal(Baht(json.FinalBalance), ws.Cell(ana, 2).GetValue<decimal>());
        Assert.Equal($"{json.Distribution.Partner1.Name} (%90)", ws.Cell(ana + 1, 1).GetString());
        Assert.Equal($"{json.Distribution.Partner2.Name} (%10)", ws.Cell(ana + 2, 1).GetString());
        var partner1 = ws.Cell(ana + 1, 2).GetValue<decimal>();
        var partner2 = ws.Cell(ana + 2, 2).GetValue<decimal>();
        Assert.Equal(Baht(json.Distribution.Partner1.AmountSatang), partner1);
        Assert.Equal(Baht(json.Distribution.Partner2.AmountSatang), partner2);
        Assert.Equal(Baht(json.FinalBalance), partner1 + partner2);

        // Sheet2: satır sayısı = başlık + ayın işlem sayısı; ilk satır tarih+CreatedAt sırasına uyar
        var txns = wb.Worksheet("İşlemler");
        Assert.Equal(1 + 10, txns.LastRowUsed()!.RowNumber());
        Assert.Equal(goldenDate.ToDateTime(TimeOnly.MinValue), txns.Cell(2, 1).GetDateTime());
        Assert.Equal("Gelir", txns.Cell(2, 2).GetString());
        Assert.Equal("Kiralama", txns.Cell(2, 3).GetString());
        Assert.Equal("Nakit", txns.Cell(2, 4).GetString());
        Assert.Equal(1000.00m, txns.Cell(2, 5).GetValue<decimal>());
        Assert.Equal("Honda Click 3 gün", txns.Cell(2, 6).GetString());

        // Sheet3: kategori toplamları JSON ile eşleşir (önce Gelir bloğu, sonra Gider)
        var cats = wb.Worksheet("Kategori Dağılımı");
        var lastCat = cats.LastRowUsed()!.RowNumber();
        Assert.Equal(1 + json.IncomeByCategory.Count + json.ExpenseByCategory.Count, lastCat);
        foreach (var (type, expected) in new[] { ("Gelir", json.IncomeByCategory), ("Gider", json.ExpenseByCategory) })
            foreach (var cat in expected)
            {
                var row = Enumerable.Range(2, lastCat - 1).Single(r =>
                    cats.Cell(r, 1).GetString() == type && cats.Cell(r, 2).GetString() == cat.Category);
                Assert.Equal(Baht(cat.TotalSatang), cats.Cell(row, 3).GetValue<decimal>());
            }
    }

    // ── Negatif ay: dağıtım satırları yine yazılır (ham veri; gizleme UI kuralı) ───────────

    [Fact]
    public async Task Xlsx_NegativeFinalBalance_StillWritesDistributionRows()
    {
        await PostTransactionAsync("2026-03-10", "Expense", YakitAlimId, "Cash", "500");

        var json = await GetMonthJsonAsync("2026-03");
        Assert.True(json.FinalBalance < 0);

        using var wb = await GetWorkbookAsync("2026-03");
        var ws = wb.Worksheet("Ay Özeti");

        var ana = FindLabelRow(ws, "Ay Sonu Ana Kasa");
        Assert.Equal(-500.00m, ws.Cell(ana, 2).GetValue<decimal>());
        var partner1 = ws.Cell(ana + 1, 2).GetValue<decimal>();
        var partner2 = ws.Cell(ana + 2, 2).GetValue<decimal>();
        Assert.Equal(-450.00m, partner1); // -50000 × 0.90 (AwayFromZero)
        Assert.Equal(-50.00m, partner2);  // kalan; toplam bakiyeye eşit, satang kaybolmaz
        Assert.Equal(Baht(json.FinalBalance), partner1 + partner2);
    }

    // ── İşlemsiz ay: 200 + başlıklar + sıfır toplamlar içeren geçerli dosya ────────────────

    [Fact]
    public async Task Xlsx_EmptyMonth_Returns200WithValidWorkbookAndZeroTotals()
    {
        using var wb = await GetWorkbookAsync("2026-02");

        Assert.Equal(
            ["Ay Özeti", "İşlemler", "Kategori Dağılımı"],
            wb.Worksheets.Select(w => w.Name).ToArray());

        var ws = wb.Worksheet("Ay Özeti");
        Assert.Equal("Tarih", ws.Cell(1, 1).GetString());
        Assert.Equal("Kiralama %", ws.Cell(1, 7).GetString());

        var total = FindLabelRow(ws, "TOPLAM");
        for (var col = 2; col <= 6; col++)
            Assert.Equal(0m, ws.Cell(total, col).GetValue<decimal>());

        var ana = FindLabelRow(ws, "Ay Sonu Ana Kasa");
        Assert.Equal(0m, ws.Cell(ana, 2).GetValue<decimal>());

        // Sheet2 ve Sheet3 yalnızca başlık satırı içerir
        Assert.Equal(1, wb.Worksheet("İşlemler").LastRowUsed()!.RowNumber());
        Assert.Equal(1, wb.Worksheet("Kategori Dağılımı").LastRowUsed()!.RowNumber());
    }

    // ── Geçersiz/eksik month: 400 + Türkçe mesaj ───────────────────────────────────────────

    [Theory]
    [InlineData("/api/reports/month/xlsx")]
    [InlineData("/api/reports/month/xlsx?month=2026-13")]
    [InlineData("/api/reports/month/xlsx?month=temmuz")]
    public async Task Xlsx_MissingOrInvalidMonth_Returns400WithTurkishMessage(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(KasaApiFactory.Json);
        Assert.NotNull(body);
        Assert.Contains("month parametresi zorunludur", body["error"]);
    }
}
