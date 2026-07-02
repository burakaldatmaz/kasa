using System.Net;
using System.Net.Http.Json;
using Kasa.Api.Contracts;
using Kasa.Domain;

namespace Kasa.Tests;

public class CategoryApiTests(KasaApiFactory factory) : IClassFixture<KasaApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_ReturnsSeededCategories()
    {
        var income = await _client.GetFromJsonAsync<List<CategoryResponse>>(
            "/api/categories?type=Income", KasaApiFactory.Json);
        var expense = await _client.GetFromJsonAsync<List<CategoryResponse>>(
            "/api/categories?type=Expense", KasaApiFactory.Json);

        Assert.NotNull(income);
        Assert.NotNull(expense);
        Assert.Contains(income, c => c.Name == "Kiralama");
        Assert.Contains(expense, c => c.Name == "Servis Bakım");
        // "Diğer" her iki türde de var: unique kısıtı tür bazlıdır.
        Assert.Contains(income, c => c.Name == "Diğer");
        Assert.Contains(expense, c => c.Name == "Diğer");
    }

    [Fact]
    public async Task Post_DuplicateNameInSameType_CaseInsensitive_Returns400()
    {
        // Seed'de "Kiralama" (Income) var.
        var response = await _client.PostAsJsonAsync(
            "/api/categories", new { name = "kiralama", type = "Income" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(KasaApiFactory.Json);
        Assert.NotNull(error);
        Assert.Contains("zaten var", error.Error);
    }

    [Fact]
    public async Task Post_SameNameInOtherType_Succeeds()
    {
        // "Nakliye" seed'de sadece Expense'te var; Income'a eklenebilmeli.
        var response = await _client.PostAsJsonAsync(
            "/api/categories", new { name = "Nakliye", type = "Income" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CategoryResponse>(KasaApiFactory.Json);
        Assert.NotNull(created);
        Assert.Equal(TransactionType.Income, created.Type);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task Deactivated_HiddenByDefault_VisibleWithIncludeInactive()
    {
        var create = await _client.PostAsJsonAsync(
            "/api/categories", new { name = "Geçici Kategori", type = "Expense" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var category = await create.Content.ReadFromJsonAsync<CategoryResponse>(KasaApiFactory.Json);
        Assert.NotNull(category);

        var deactivate = await _client.PutAsJsonAsync(
            $"/api/categories/{category.Id}", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var defaultList = await _client.GetFromJsonAsync<List<CategoryResponse>>(
            "/api/categories?type=Expense", KasaApiFactory.Json);
        Assert.NotNull(defaultList);
        Assert.DoesNotContain(defaultList, c => c.Id == category.Id);

        var fullList = await _client.GetFromJsonAsync<List<CategoryResponse>>(
            "/api/categories?type=Expense&includeInactive=true", KasaApiFactory.Json);
        Assert.NotNull(fullList);
        var found = Assert.Single(fullList, c => c.Id == category.Id);
        Assert.False(found.IsActive);
    }

    [Fact]
    public async Task Put_RenameAndReorder_Works()
    {
        var create = await _client.PostAsJsonAsync(
            "/api/categories", new { name = "Eski İsim", type = "Income" });
        var category = await create.Content.ReadFromJsonAsync<CategoryResponse>(KasaApiFactory.Json);
        Assert.NotNull(category);

        var put = await _client.PutAsJsonAsync(
            $"/api/categories/{category.Id}", new { name = "Yeni İsim", sortOrder = 99 });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await put.Content.ReadFromJsonAsync<CategoryResponse>(KasaApiFactory.Json);
        Assert.NotNull(updated);
        Assert.Equal("Yeni İsim", updated.Name);
        Assert.Equal(99, updated.SortOrder);
        Assert.True(updated.IsActive); // gönderilmeyen alan değişmez
    }

    [Fact]
    public async Task Put_RenameToExistingNameInType_Returns400()
    {
        var create = await _client.PostAsJsonAsync(
            "/api/categories", new { name = "Çakışma Adayı", type = "Income" });
        var category = await create.Content.ReadFromJsonAsync<CategoryResponse>(KasaApiFactory.Json);
        Assert.NotNull(category);

        // Seed'de "Hasar" (Income) var; case-insensitive çakışma reddedilmeli.
        var put = await _client.PutAsJsonAsync(
            $"/api/categories/{category.Id}", new { name = "HASAR" });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/categories/99999", new { isActive = false });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
