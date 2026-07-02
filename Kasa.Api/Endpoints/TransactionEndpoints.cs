using Kasa.Api.Contracts;
using Kasa.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Kasa.Api.Endpoints;

public static class TransactionEndpoints
{
    public static void MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions");

        group.MapGet("/", async (KasaDbContext db, DateOnly? date) =>
        {
            if (date is null)
                return Results.BadRequest(new ErrorResponse("date parametresi zorunludur (örn: ?date=2026-07-02)."));

            var transactions = await db.Transactions.AsNoTracking()
                .Where(t => t.Date == date)
                .OrderBy(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .Select(t => new TransactionResponse(
                    t.Id, t.Date, t.Type, t.CategoryId, t.Category!.Name,
                    t.PaymentMethod, t.AmountSatang, t.Note, t.CreatedAt))
                .ToListAsync();

            return Results.Ok(transactions);
        });

        group.MapPost("/", async (KasaDbContext db, SaveTransactionRequest request) =>
        {
            var validation = await ValidateAsync(db, request, existing: null);
            if (validation.Error is not null)
                return validation.Error;

            var transaction = new Transaction
            {
                Date = request.Date,
                Type = request.Type,
                CategoryId = request.CategoryId,
                PaymentMethod = request.PaymentMethod,
                AmountSatang = validation.AmountSatang,
                Note = NormalizeNote(request.Note),
                CreatedAt = DateTime.UtcNow
            };
            db.Transactions.Add(transaction);
            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/transactions/{transaction.Id}",
                ToResponse(transaction, validation.CategoryName!));
        });

        group.MapPut("/{id:int}", async (KasaDbContext db, int id, SaveTransactionRequest request) =>
        {
            var transaction = await db.Transactions.FindAsync(id);
            if (transaction is null)
                return Results.NotFound(new ErrorResponse("İşlem bulunamadı."));

            var validation = await ValidateAsync(db, request, existing: transaction);
            if (validation.Error is not null)
                return validation.Error;

            transaction.Date = request.Date;
            transaction.Type = request.Type;
            transaction.CategoryId = request.CategoryId;
            transaction.PaymentMethod = request.PaymentMethod;
            transaction.AmountSatang = validation.AmountSatang;
            transaction.Note = NormalizeNote(request.Note);
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(transaction, validation.CategoryName!));
        });

        // Hard delete bilinçli: işlem satırı düzeltme aracıdır, kategori gibi arşiv değeri yok.
        group.MapDelete("/{id:int}", async (KasaDbContext db, int id) =>
        {
            var transaction = await db.Transactions.FindAsync(id);
            if (transaction is null)
                return Results.NotFound(new ErrorResponse("İşlem bulunamadı."));

            db.Transactions.Remove(transaction);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private readonly record struct ValidationResult(IResult? Error, long AmountSatang, string? CategoryName);

    private static async Task<ValidationResult> ValidateAsync(
        KasaDbContext db, SaveTransactionRequest request, Transaction? existing)
    {
        if (request.Date == default)
            return Fail("Tarih zorunludur (örn: 2026-07-02).");

        if (!BahtParser.TryParse(request.Amount, out var amountSatang, out var amountError))
            return Fail(amountError);

        if (request.Note is not null && request.Note.Length > 500)
            return Fail("Not en fazla 500 karakter olabilir.");

        var category = await db.Categories.FindAsync(request.CategoryId);
        if (category is null)
            return Fail("Kategori bulunamadı.");

        if (category.Type != request.Type)
            return Fail("Kategori türü işlem türüyle uyuşmuyor: gider kategorisine gelir (veya tersi) yazılamaz.");

        // Pasif kategoriye YENİ işlem yazılamaz; mevcut işlemin kategorisi değişmiyorsa düzenleme serbest.
        var categoryChanged = existing is null || existing.CategoryId != request.CategoryId;
        if (categoryChanged && !category.IsActive)
            return Fail("Pasif kategoriye yeni işlem eklenemez.");

        return new ValidationResult(null, amountSatang, category.Name);

        static ValidationResult Fail(string message) =>
            new(Results.BadRequest(new ErrorResponse(message)), 0, null);
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static TransactionResponse ToResponse(Transaction t, string categoryName) =>
        new(t.Id, t.Date, t.Type, t.CategoryId, categoryName,
            t.PaymentMethod, t.AmountSatang, t.Note, t.CreatedAt);
}
