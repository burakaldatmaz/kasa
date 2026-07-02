using System.Net;
using System.Net.Http.Json;
using UglyToad.PdfPig;

namespace Kasa.Tests;

/// <summary>
/// /api/reports/daily/pdf uçtan uca testleri. Sayfa sayısı ve metin doğrulaması
/// PdfPig ile üretilen gerçek PDF üzerinden yapılır — "tek sayfa" kuralı (Bordro
/// payslip kuralı) 65 kalemlik stres gününde bile korunmalıdır.
/// Her test kendi ayında çalışır; devir zincirleri birbirine karışmaz.
/// </summary>
public class ReportPdfApiTests(KasaApiFactory factory) : IClassFixture<KasaApiFactory>
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

    private async Task<byte[]> GetPdfBytesAsync(string date)
    {
        var response = await _client.GetAsync($"/api/reports/daily/pdf?date={date}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        return bytes;
    }

    private static (int Pages, string Text) ParsePdf(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        // Kelime araları font ölçümüne göre değişebildiğinden metin boşluksuz karşılaştırılır.
        var text = string.Concat(pdf.GetPages().Select(p => p.Text))
            .Replace(" ", string.Empty);
        return (pdf.NumberOfPages, text);
    }

    // ── Sözleşme: 200 + application/pdf + boş olmayan gövde + dosya adı ────────────────────

    [Fact]
    public async Task Pdf_Returns200_WithPdfContentTypeAndFileName()
    {
        var response = await _client.GetAsync("/api/reports/daily/pdf?date=2026-12-05");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("kasa-islem-2026-12-05.pdf", response.Content.Headers.ContentDisposition?.FileName);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    // ── Golden gün: tek sayfa + JSON raporuyla birebir aynı rakamlar + Türkçe karakterler ──

    [Fact]
    public async Task Pdf_GoldenDay_IsSinglePage_AndMatchesJsonReport()
    {
        var date = "2026-08-03"; // kasa.db'deki golden günün birebir kopyası

        await PostTransactionAsync(date, "Income", KiralamaId, "Cash", "1000", "Honda Click 3 gün");
        await PostTransactionAsync(date, "Income", KiralamaId, "Cash", "800");
        await PostTransactionAsync(date, "Income", KiralamaId, "CreditCard", "3500", "Haftalık kiralama");
        await PostTransactionAsync(date, "Income", EksikYakitId, "Cash", "200");
        await PostTransactionAsync(date, "Income", KiralamaUzatmaId, "BankTransfer", "600");
        await PostTransactionAsync(date, "Expense", KiraId, "BankTransfer", "1000", "Dükkan kirası taksit");
        await PostTransactionAsync(date, "Expense", YakitAlimId, "Cash", "500");
        await PostTransactionAsync(date, "Expense", ServisBakimId, "BankTransfer", "2300", "Fren balata + yağ");
        await PostTransactionAsync(date, "Expense", MaasId, "Cash", "2000");

        var fleet = await _client.PutAsJsonAsync($"/api/fleet/{date}",
            new { totalBikes = 14, brokenBikes = 4, rentedBikes = 8 });
        Assert.Equal(HttpStatusCode.OK, fleet.StatusCode);

        var (pages, text) = ParsePdf(await GetPdfBytesAsync(date));

        Assert.Equal(1, pages);

        // JSON raporunun kanıt değerleri (GoldenDayTest ile aynı sonuç: 17750 satang = ฿177.50)
        Assert.Contains("ANAKASA฿177.50", text);
        Assert.Contains("GelirlerToplamı฿6,100.00", text);
        Assert.Contains("GiderlerToplamı฿5,800.00", text);
        Assert.Contains("POSKesintisi(%3,5)฿122.50", text); // oran Settings'ten, hardcode değil
        Assert.Contains("GünNet฿177.50", text);
        Assert.Contains("Devir(öncekigünden):฿0.00", text);

        // Türkçe karakterler PDF'e doğru gömülmüş olmalı (İ, ğ, ş, ı)
        Assert.Contains("KASAİŞLEM—3Ağustos2026", text);
        Assert.Contains("EksikYakıtTahsilatı", text);
        Assert.Contains("Frenbalata+yağ", text);
        Assert.Contains("Maaş", text);

        // Filo satırı snapshot'tan gelir
        Assert.Contains("FİLO:Toplam14", text);
        Assert.Contains("Kiralama%80.0", text);
    }

    // ── Stres: 65 kalemlik sentetik gün yine TEK sayfa ─────────────────────────────────────

    [Fact]
    public async Task Pdf_65LineDay_IsStillSinglePage()
    {
        var date = "2026-10-15";
        int[] incomeCategories = [KiralamaId, EksikYakitId, KiralamaUzatmaId];
        int[] expenseCategories = [ServisBakimId, YakitAlimId, KiraId, MaasId];
        string[] methods = ["Cash", "CreditCard", "BankTransfer"];

        for (var i = 0; i < 65; i++)
        {
            var income = i % 2 == 0;
            await PostTransactionAsync(
                date,
                income ? "Income" : "Expense",
                income ? incomeCategories[i % incomeCategories.Length] : expenseCategories[i % expenseCategories.Length],
                methods[i % methods.Length],
                (100 + i).ToString(),
                i % 3 == 0 ? $"Sentetik not {i} — ğüşiöç İĞÜŞÖÇ" : null);
        }

        var (pages, text) = ParsePdf(await GetPdfBytesAsync(date));

        Assert.Equal(1, pages);
        Assert.Contains("ANAKASA", text);
    }

    // ── İşlemsiz gün: PDF yine üretilir, boş bölümler ve devir görünür ─────────────────────

    [Fact]
    public async Task Pdf_EmptyDay_StillRendersWithCarryOver()
    {
        // 10 Kasım'a gelir girilir; 12 Kasım işlemsizdir ama deviri taşır
        await PostTransactionAsync("2026-11-10", "Income", KiralamaId, "Cash", "400");

        var (pages, text) = ParsePdf(await GetPdfBytesAsync("2026-11-12"));

        Assert.Equal(1, pages);
        Assert.Contains("—Kayıtyok—", text);                    // boş GELİR/GİDER bölümleri
        Assert.Contains("Devir(öncekigünden):฿400.00", text);   // devir görünür
        Assert.Contains("ANAKASA฿400.00", text);
        Assert.Contains("Filoverisigirilmedi", text);           // snapshot yok
    }

    // ── Geçersiz/eksik tarih: 400 + Türkçe mesaj ───────────────────────────────────────────

    [Theory]
    [InlineData("/api/reports/daily/pdf")]
    [InlineData("/api/reports/daily/pdf?date=abc")]
    [InlineData("/api/reports/daily/pdf?date=2026-13-99")]
    [InlineData("/api/reports/daily/pdf?date=03-08-2026")]
    public async Task Pdf_MissingOrInvalidDate_Returns400WithTurkishMessage(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(KasaApiFactory.Json);
        Assert.NotNull(body);
        Assert.Contains("date parametresi zorunludur", body["error"]);
    }
}
