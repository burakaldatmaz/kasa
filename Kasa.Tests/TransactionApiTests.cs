using System.Net;
using System.Net.Http.Json;
using Kasa.Api.Contracts;
using Kasa.Domain;

namespace Kasa.Tests;

public class TransactionApiTests(KasaApiFactory factory) : IClassFixture<KasaApiFactory>
{
    // Seed Id'leri deterministik (migration): 1 = Kiralama (Income), 7 = Servis Bakım (Expense).
    private const int KiralamaId = 1;
    private const int ServisBakimId = 7;

    private readonly HttpClient _client = factory.CreateClient();

    private static object Request(
        string amount,
        int categoryId = KiralamaId,
        string type = "Income",
        string date = "2026-07-01",
        string paymentMethod = "Cash",
        string? note = null) =>
        new { date, type, categoryId, paymentMethod, amount, note };

    [Theory]
    [InlineData("2300", 230_000L)]
    [InlineData("2300.5", 230_050L)]
    [InlineData("2300.50", 230_050L)]
    [InlineData("0.01", 1L)]
    public async Task Post_ValidBahtAmount_ConvertsToSatang(string amount, long expectedSatang)
    {
        var response = await _client.PostAsJsonAsync("/api/transactions", Request(amount));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TransactionResponse>(KasaApiFactory.Json);
        Assert.NotNull(created);
        Assert.Equal(expectedSatang, created.AmountSatang);
    }

    [Theory]
    [InlineData("2300.505")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task Post_InvalidBahtAmount_Returns400WithTurkishMessage(string amount)
    {
        var response = await _client.PostAsJsonAsync("/api/transactions", Request(amount));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Contains("utar", error.Error); // "Geçersiz tutar..." / "Tutar sıfırdan büyük..."
    }

    [Fact]
    public async Task Post_IncomeTypeWithExpenseCategory_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/transactions", Request("100", categoryId: ServisBakimId, type: "Income"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Contains("uyuşmuyor", error.Error);
    }

    [Fact]
    public async Task Post_ToInactiveCategory_Returns400_ButExistingTransactionsStayReadable()
    {
        // Kendi kategorimizi kurup içine bir işlem yazalım, sonra pasifleştirelim.
        var createCategory = await _client.PostAsJsonAsync(
            "/api/categories", new { name = "Pasiflik Testi", type = "Expense" });
        Assert.Equal(HttpStatusCode.Created, createCategory.StatusCode);
        var category = await createCategory.Content.ReadFromJsonAsync<CategoryResponse>(KasaApiFactory.Json);
        Assert.NotNull(category);

        var date = "2026-06-15";
        var firstPost = await _client.PostAsJsonAsync(
            "/api/transactions", Request("50", categoryId: category.Id, type: "Expense", date: date));
        Assert.Equal(HttpStatusCode.Created, firstPost.StatusCode);

        var deactivate = await _client.PutAsJsonAsync(
            $"/api/categories/{category.Id}", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        // Pasif kategoriye YENİ işlem reddedilir...
        var secondPost = await _client.PostAsJsonAsync(
            "/api/transactions", Request("60", categoryId: category.Id, type: "Expense", date: date));
        Assert.Equal(HttpStatusCode.BadRequest, secondPost.StatusCode);
        var error = await secondPost.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Contains("Pasif", error.Error);

        // ...mevcut işlem okunmaya devam eder.
        var list = await _client.GetFromJsonAsync<List<TransactionResponse>>(
            $"/api/transactions?date={date}", KasaApiFactory.Json);
        Assert.NotNull(list);
        Assert.Single(list, t => t.CategoryId == category.Id && t.AmountSatang == 5_000L);
    }

    [Fact]
    public async Task Get_ReturnsTransactionsInCreationOrder()
    {
        var date = "2026-06-20";
        foreach (var amount in new[] { "10", "20", "30" })
        {
            var post = await _client.PostAsJsonAsync(
                "/api/transactions", Request(amount, date: date));
            Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        }

        var list = await _client.GetFromJsonAsync<List<TransactionResponse>>(
            $"/api/transactions?date={date}", KasaApiFactory.Json);

        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.Equal([1_000L, 2_000L, 3_000L], list.Select(t => t.AmountSatang).ToArray());
    }

    [Fact]
    public async Task Get_WithoutDate_Returns400()
    {
        var response = await _client.GetAsync("/api/transactions");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTransaction()
    {
        var date = "2026-06-25";
        var post = await _client.PostAsJsonAsync("/api/transactions", Request("75", date: date));
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<TransactionResponse>(KasaApiFactory.Json);
        Assert.NotNull(created);

        var delete = await _client.DeleteAsync($"/api/transactions/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await _client.GetFromJsonAsync<List<TransactionResponse>>(
            $"/api/transactions?date={date}", KasaApiFactory.Json);
        Assert.NotNull(list);
        Assert.DoesNotContain(list, t => t.Id == created.Id);
    }

    [Fact]
    public async Task Put_UpdatesAmountAndNote()
    {
        var date = "2026-06-26";
        var post = await _client.PostAsJsonAsync("/api/transactions", Request("100", date: date));
        var created = await post.Content.ReadFromJsonAsync<TransactionResponse>(KasaApiFactory.Json);
        Assert.NotNull(created);

        var put = await _client.PutAsJsonAsync(
            $"/api/transactions/{created.Id}",
            Request("150.25", date: date, note: "düzeltme"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await put.Content.ReadFromJsonAsync<TransactionResponse>(KasaApiFactory.Json);
        Assert.NotNull(updated);
        Assert.Equal(15_025L, updated.AmountSatang);
        Assert.Equal("düzeltme", updated.Note);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
    }
}
