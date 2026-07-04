using System.Net;
using System.Net.Http.Json;
using Kasa.Api.Contracts;
using UglyToad.PdfPig;

namespace Kasa.Tests;

/// <summary>
/// /api/deposit-receipts uçtan uca: numara üretimi (yıl bazlı MAX+1, yıl sınırında sıfırlama,
/// eşzamanlı POST çakışmaz), gün listesi (yeniden yazdırma) ve PDF (tek sayfa + Sarabun Thai
/// dizgisi PdfPig ile doğrulanır). Kasa'nın mali akışına dokunulmadığı için mevcut süit etkilenmez.
/// </summary>
public class DepositReceiptApiTests(KasaApiFactory factory) : IClassFixture<KasaApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object Body(
        string date = "2026-07-03",
        string customer = "Edward Penney Beaumont",
        string vehicle = "Honda Click 160",
        string? color = "Matte Black",
        string plate = "8 ขผ 7250",
        string amount = "3000",
        string paymentMethod = "Cash",
        string? fuel = "Full",
        string handoverAt = "2026-07-03T11:00:00",
        string returnExpectedAt = "2026-08-02T11:00:00",
        int? limitKmPerDay = 150,
        int? limitRadiusKm = 150) => new
        {
            date,
            customerName = customer,
            vehicleModel = vehicle,
            vehicleColor = color,
            plate,
            amount,
            paymentMethod,
            fuelLevel = fuel,
            handoverAt,
            returnExpectedAt,
            limitKmPerDay,
            limitRadiusKm
        };

    private async Task<DepositReceiptResponse> PostAsync(object body)
    {
        var response = await _client.PostAsJsonAsync("/api/deposit-receipts", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DepositReceiptResponse>(KasaApiFactory.Json);
        Assert.NotNull(created);
        return created!;
    }

    private static (int Pages, string Text) ParsePdf(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        var text = string.Concat(pdf.GetPages().Select(p => p.Text)).Replace(" ", string.Empty);
        return (pdf.NumberOfPages, text);
    }

    // ── Numara: server atar, yıl bazlı 1'den başlar ve artar ───────────────────────────────
    // Not: IClassFixture ile DB paylaşımlı; numara yıl bazlı olduğundan her test kendi yılını
    // kullanır ki sıralar birbirine karışmasın.
    [Fact]
    public async Task Post_AssignsServerNumber_StartingAtOne()
    {
        var first = await PostAsync(Body(date: "2040-03-01"));
        var second = await PostAsync(Body(date: "2040-03-02"));

        Assert.Equal("DEP-2040-00001", first.No);
        Assert.Equal("DEP-2040-00002", second.No);
        Assert.True(first.Id > 0);
        Assert.Equal(300000, first.AmountSatang); // 3000 baht → satang (I1)
    }

    // ── Yıl sınırında sıra sıfırlanır ──────────────────────────────────────────────────────
    [Fact]
    public async Task Post_ResetsSequence_AtYearBoundary()
    {
        await PostAsync(Body(date: "2028-12-31"));
        var next2028 = await PostAsync(Body(date: "2028-12-31"));
        var first2029 = await PostAsync(Body(date: "2029-01-01"));

        Assert.Equal("DEP-2028-00002", next2028.No);
        Assert.Equal("DEP-2029-00001", first2029.No);
    }

    // ── Eşzamanlı POST çakışmaz: hepsi 201, numaralar tekil ─────────────────────────────────
    [Fact]
    public async Task Post_Concurrent_ProducesUniqueNumbers()
    {
        const int n = 12;
        var tasks = Enumerable.Range(0, n)
            .Select(_ => PostAsync(Body(date: "2030-05-05")))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        var numbers = results.Select(r => r.No).ToHashSet();
        Assert.Equal(n, numbers.Count); // hiçbir numara tekrar etmedi
        Assert.All(numbers, no => Assert.StartsWith("DEP-2030-", no));
    }

    // ── Gün listesi: yalnız o günün makbuzları, No sırasıyla (yeniden yazdırma) ─────────────
    [Fact]
    public async Task Get_ByDate_ListsThatDaysReceipts_InOrder()
    {
        await PostAsync(Body(date: "2041-09-10", customer: "Aylin One"));
        await PostAsync(Body(date: "2041-09-10", customer: "Berk Two"));
        await PostAsync(Body(date: "2041-09-11", customer: "Other Day"));

        var response = await _client.GetAsync("/api/deposit-receipts?date=2041-09-10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<DepositReceiptResponse>>(KasaApiFactory.Json);

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Equal(["Aylin One", "Berk Two"], list.Select(r => r.CustomerName));
        Assert.True(string.CompareOrdinal(list[0].No, list[1].No) < 0); // No sırasında
    }

    [Fact]
    public async Task Get_ByDate_MissingParam_Returns400()
    {
        var response = await _client.GetAsync("/api/deposit-receipts");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(KasaApiFactory.Json);
        Assert.Contains("date parametresi zorunludur", body!["error"]);
    }

    // ── PDF: tek sayfa + numara/isim/tutar/Thai dizgi (Sarabun gömülü) ──────────────────────
    [Fact]
    public async Task Pdf_IsSinglePage_WithNumberNameAmountAndThaiWords()
    {
        var created = await PostAsync(Body(date: "2042-06-20", customer: "Edward Penney Beaumont", amount: "3000"));

        var response = await _client.GetAsync($"/api/deposit-receipts/{created.Id}/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"{created.No}.pdf", response.Content.Headers.ContentDisposition?.FileName);

        var (pages, text) = ParsePdf(await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(1, pages); // iki nüsha tek A4'te (payslip kuralı)
        Assert.Contains(created.No.Replace(" ", string.Empty), text);
        Assert.Contains("EdwardPenneyBeaumont", text);
        Assert.Contains("฿3,000.00", text);
        Assert.Contains("Threethousandbahtonly", text);        // İngilizce (Faz 12)
        Assert.Contains("สามพันบาทถ้วน", text);                   // Thai — Sarabun doğru gömülmüş
        Assert.Contains("DAMAGEDEPOSITRECEIPT", text);
        Assert.Contains("CUSTOMERCOPY", text);
        Assert.Contains("OFFICECOPY", text);
    }

    [Fact]
    public async Task Pdf_MissingReceipt_Returns404()
    {
        var response = await _client.GetAsync("/api/deposit-receipts/999999/pdf");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Doğrulama: eksik zorunlu alanlar 400 + Türkçe mesaj ─────────────────────────────────
    [Theory]
    [InlineData("customerName", "Müşteri adı zorunludur.")]
    [InlineData("vehicleModel", "Araç modeli zorunludur.")]
    [InlineData("plate", "Plaka zorunludur.")]
    public async Task Post_MissingRequiredField_Returns400(string field, string expected)
    {
        var body = new Dictionary<string, object?>
        {
            ["date"] = "2026-07-03",
            ["customerName"] = "X",
            ["vehicleModel"] = "Y",
            ["plate"] = "Z",
            ["amount"] = "3000",
            ["paymentMethod"] = "Cash",
            ["handoverAt"] = "2026-07-03T11:00:00",
            ["returnExpectedAt"] = "2026-08-02T11:00:00"
        };
        body[field] = "   "; // boşluk → zorunlu alan boş sayılır

        var response = await _client.PostAsJsonAsync("/api/deposit-receipts", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(KasaApiFactory.Json);
        Assert.Equal(expected, error!["error"]);
    }

    [Fact]
    public async Task Post_InvalidAmount_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/deposit-receipts", Body(amount: "-5"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
