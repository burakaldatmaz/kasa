using Kasa.Domain;

namespace Kasa.Api.Contracts;

/// <summary>Amount BAHT string'dir ("2300" / "2300.50"); satang'a çeviri server'da yapılır (I1).</summary>
public record SaveTransactionRequest(
    DateOnly Date,
    TransactionType Type,
    int CategoryId,
    PaymentMethod PaymentMethod,
    string? Amount,
    string? Note);

public record TransactionResponse(
    int Id,
    DateOnly Date,
    TransactionType Type,
    int CategoryId,
    string CategoryName,
    PaymentMethod PaymentMethod,
    long AmountSatang,
    string? Note,
    DateTime CreatedAt);

public record ErrorResponse(string Error);
