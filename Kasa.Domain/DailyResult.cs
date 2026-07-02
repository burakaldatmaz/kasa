namespace Kasa.Domain;

/// <summary>Bir günün hesaplanmış sonucu. Tüm değerler satang cinsinden.</summary>
public readonly record struct DailyResult(
    Money IncomeTotal,
    Money ExpenseTotal,
    Money PosFee,
    Money DayNet,
    Money ClosingBalance);
