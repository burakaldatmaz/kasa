using Kasa.Api.Contracts;
using Kasa.Api.Data;
using Kasa.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kasa.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories");

        group.MapGet("/", async (KasaDbContext db, TransactionType? type, bool includeInactive = false) =>
        {
            var query = db.Categories.AsNoTracking();
            if (type is not null)
                query = query.Where(c => c.Type == type);
            if (!includeInactive)
                query = query.Where(c => c.IsActive);

            var categories = await query
                .OrderBy(c => c.Type)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .Select(c => new CategoryResponse(c.Id, c.Name, c.Type, c.IsActive, c.SortOrder))
                .ToListAsync();

            return Results.Ok(categories);
        });

        group.MapPost("/", async (KasaDbContext db, CreateCategoryRequest request) =>
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrEmpty(name))
                return Results.BadRequest(new ErrorResponse("Kategori adı boş olamaz."));
            if (name.Length > 100)
                return Results.BadRequest(new ErrorResponse("Kategori adı en fazla 100 karakter olabilir."));

            if (await NameExistsInTypeAsync(db, request.Type, name, excludeId: null))
                return Results.BadRequest(new ErrorResponse("Bu türde aynı isimde bir kategori zaten var."));

            var maxSortOrder = await db.Categories
                .Where(c => c.Type == request.Type)
                .Select(c => (int?)c.SortOrder)
                .MaxAsync() ?? 0;

            var category = new Category
            {
                Name = name,
                Type = request.Type,
                IsActive = true,
                SortOrder = maxSortOrder + 1
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/categories/{category.Id}",
                new CategoryResponse(category.Id, category.Name, category.Type, category.IsActive, category.SortOrder));
        });

        // Kategoride hard delete yok: kaldırma işi IsActive=false ile yapılır (soft delete).
        group.MapPut("/{id:int}", async (KasaDbContext db, int id, UpdateCategoryRequest request) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null)
                return Results.NotFound(new ErrorResponse("Kategori bulunamadı."));

            if (request.Name is not null)
            {
                var name = request.Name.Trim();
                if (name.Length == 0)
                    return Results.BadRequest(new ErrorResponse("Kategori adı boş olamaz."));
                if (name.Length > 100)
                    return Results.BadRequest(new ErrorResponse("Kategori adı en fazla 100 karakter olabilir."));
                if (await NameExistsInTypeAsync(db, category.Type, name, excludeId: category.Id))
                    return Results.BadRequest(new ErrorResponse("Bu türde aynı isimde bir kategori zaten var."));
                category.Name = name;
            }

            if (request.SortOrder is not null)
                category.SortOrder = request.SortOrder.Value;
            if (request.IsActive is not null)
                category.IsActive = request.IsActive.Value;

            await db.SaveChangesAsync();

            return Results.Ok(
                new CategoryResponse(category.Id, category.Name, category.Type, category.IsActive, category.SortOrder));
        });
    }

    // Tablo küçük olduğu için isim karşılaştırması bellekte yapılır: SQLite LOWER() yalnızca
    // ASCII katlar, OrdinalIgnoreCase Türkçe karakterlerde daha tutarlıdır.
    private static async Task<bool> NameExistsInTypeAsync(KasaDbContext db, TransactionType type, string name, int? excludeId)
    {
        var names = await db.Categories
            .Where(c => c.Type == type && (excludeId == null || c.Id != excludeId))
            .Select(c => c.Name)
            .ToListAsync();
        return names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }
}
