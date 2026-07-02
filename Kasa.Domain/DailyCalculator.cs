namespace Kasa.Domain;

/// <summary>
/// Günlük kasa hesaplayıcısı. Saf ve durumsuz (I2): dış bağımlılık yok, yan etki yok.
/// </summary>
public static class DailyCalculator
{
    /// <summary>
    /// Kurallar:
    ///   PosFee = SADECE Income + CreditCard satırları toplamı × posFeeRate (AwayFromZero, satang'a yuvarlı)
    ///   DayNet = IncomeTotal − ExpenseTotal − PosFee
    ///   ClosingBalance = previousBalance + DayNet (negatif olabilir)
    /// </summary>
    public static DailyResult Calculate(
        IReadOnlyList<TxnLine> lines,
        decimal posFeeRate,
        long previousBalanceSatang)
    {
        var incomeTotal = Money.Zero;
        var expenseTotal = Money.Zero;
        var creditCardIncome = Money.Zero;

        foreach (var line in lines)
        {
            var amount = new Money(line.AmountSatang);

            if (line.Type == TransactionType.Income)
            {
                incomeTotal += amount;
                if (line.PaymentMethod == PaymentMethod.CreditCard)
                    creditCardIncome += amount;
            }
            else
            {
                expenseTotal += amount;
            }
        }

        var posFee = creditCardIncome.Percent(posFeeRate);
        var dayNet = incomeTotal - expenseTotal - posFee;
        var closingBalance = new Money(previousBalanceSatang) + dayNet;

        return new DailyResult(incomeTotal, expenseTotal, posFee, dayNet, closingBalance);
    }
}
