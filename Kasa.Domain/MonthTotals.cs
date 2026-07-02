namespace Kasa.Domain;

/// <summary>
/// Ay toplamları: zincir günlerinin alan alan toplamı.
/// DayNet toplamı tanım gereği ay sonu bakiyesine eşittir (zincir ayın 1'inde 0'dan başlar).
/// </summary>
public readonly record struct MonthTotals(Money IncomeTotal, Money ExpenseTotal, Money PosFee, Money DayNet);
