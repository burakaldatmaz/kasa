using Kasa.Domain;

namespace Kasa.Tests;

public class DailyCalculatorTests
{
    [Fact]
    public void NegativeClosing_When_Expenses_Exceed_Income()
    {
        var lines = new List<TxnLine>
        {
            new(TransactionType.Income, PaymentMethod.Cash, 50000),
            new(TransactionType.Expense, PaymentMethod.Cash, 120000),
        };

        var result = DailyCalculator.Calculate(lines, posFeeRate: 0.035m, previousBalanceSatang: 30000);

        Assert.Equal(50000, result.IncomeTotal.Satang);
        Assert.Equal(120000, result.ExpenseTotal.Satang);
        Assert.Equal(0, result.PosFee.Satang);
        Assert.Equal(-70000, result.DayNet.Satang);
        Assert.Equal(-40000, result.ClosingBalance.Satang);
    }

    [Fact]
    public void CreditCard_Expense_Is_Not_Included_In_PosFee()
    {
        var lines = new List<TxnLine>
        {
            new(TransactionType.Income, PaymentMethod.CreditCard, 100000),
            // Kredi kartlı GİDER: pos fee'ye dahil edilmemeli
            new(TransactionType.Expense, PaymentMethod.CreditCard, 400000),
        };

        var result = DailyCalculator.Calculate(lines, posFeeRate: 0.035m, previousBalanceSatang: 0);

        // Sadece 100000 gelir × 0.035 = 3500; gider dahil olsaydı 17500 çıkardı
        Assert.Equal(3500, result.PosFee.Satang);
        Assert.Equal(100000 - 400000 - 3500, result.DayNet.Satang);
    }

    [Fact]
    public void PosFee_Is_Zero_When_No_CreditCard_Income()
    {
        var lines = new List<TxnLine>
        {
            new(TransactionType.Income, PaymentMethod.Cash, 100000),
            new(TransactionType.Income, PaymentMethod.BankTransfer, 200000),
        };

        var result = DailyCalculator.Calculate(lines, posFeeRate: 0.035m, previousBalanceSatang: 0);

        Assert.Equal(0, result.PosFee.Satang);
        Assert.Equal(300000, result.DayNet.Satang);
    }

    [Fact]
    public void ThreeDay_CarryOver_Chain()
    {
        // Gün 1: 1000 net
        var day1 = DailyCalculator.Calculate(
            [new(TransactionType.Income, PaymentMethod.Cash, 1000)],
            posFeeRate: 0.035m, previousBalanceSatang: 0);
        Assert.Equal(1000, day1.ClosingBalance.Satang);

        // Gün 2: 2000 gider → net -2000, kapanış -1000
        var day2 = DailyCalculator.Calculate(
            [new(TransactionType.Expense, PaymentMethod.Cash, 2000)],
            posFeeRate: 0.035m, previousBalanceSatang: day1.ClosingBalance.Satang);
        Assert.Equal(-1000, day2.ClosingBalance.Satang);

        // Gün 3: 100000 kredi kartı geliri → pos fee 3500, net 96500, kapanış 95500
        var day3 = DailyCalculator.Calculate(
            [new(TransactionType.Income, PaymentMethod.CreditCard, 100000)],
            posFeeRate: 0.035m, previousBalanceSatang: day2.ClosingBalance.Satang);
        Assert.Equal(3500, day3.PosFee.Satang);
        Assert.Equal(96500, day3.DayNet.Satang);
        Assert.Equal(95500, day3.ClosingBalance.Satang);
    }

    [Fact]
    public void EmptyDay_Carries_PreviousBalance_Unchanged()
    {
        var result = DailyCalculator.Calculate([], posFeeRate: 0.035m, previousBalanceSatang: 42000);

        Assert.Equal(0, result.DayNet.Satang);
        Assert.Equal(42000, result.ClosingBalance.Satang);
    }
}
