namespace Kasa.Domain;

/// <summary>
/// Devir zincirindeki bir gün: günün deviri (PreviousBalance) + günün hesaplanmış sonucu.
/// Result.ClosingBalance o günün kümülatif bakiyesidir.
/// </summary>
public readonly record struct ChainDay(DateOnly Date, Money PreviousBalance, DailyResult Result);
