namespace Kasa.Domain;

/// <summary>Bir günün tek işlem satırı.</summary>
public readonly record struct TxnLine(
    TransactionType Type,
    PaymentMethod PaymentMethod,
    long AmountSatang);
