using Kasa.Domain;

namespace Kasa.Tests;

/// <summary>
/// GOLDEN REGRESSION TEST (I3): Bu test hiçbir fazda değiştirilemez veya silinemez.
/// Örnek gün → DayNet tam olarak 17750 satang (177.50 THB) olmalıdır.
/// </summary>
public class GoldenDayTest
{
    [Fact]
    public void GoldenDay_DayNet_Is_Exactly_17750_Satang()
    {
        var lines = new List<TxnLine>
        {
            // Gelirler
            new(TransactionType.Income, PaymentMethod.Cash, 100000),
            new(TransactionType.Income, PaymentMethod.Cash, 80000),
            new(TransactionType.Income, PaymentMethod.CreditCard, 350000),
            new(TransactionType.Income, PaymentMethod.Cash, 20000),
            new(TransactionType.Income, PaymentMethod.BankTransfer, 60000),
            // Giderler
            new(TransactionType.Expense, PaymentMethod.BankTransfer, 100000),
            new(TransactionType.Expense, PaymentMethod.Cash, 50000),
            new(TransactionType.Expense, PaymentMethod.BankTransfer, 230000),
            new(TransactionType.Expense, PaymentMethod.Cash, 200000),
        };

        var result = DailyCalculator.Calculate(lines, posFeeRate: 0.035m, previousBalanceSatang: 0);

        Assert.Equal(610000, result.IncomeTotal.Satang);
        Assert.Equal(580000, result.ExpenseTotal.Satang);
        Assert.Equal(12250, result.PosFee.Satang);
        Assert.Equal(17750, result.DayNet.Satang);
        Assert.Equal(17750, result.ClosingBalance.Satang);
    }
}
