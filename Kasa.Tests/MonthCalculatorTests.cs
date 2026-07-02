using Kasa.Domain;

namespace Kasa.Tests;

public class MonthCalculatorTests
{
    [Fact]
    public void Split_200RandomBalances_IncludingNegatives_AlwaysSumsToFinalBalance()
    {
        var random = new Random(20260702); // deterministik tohum: test tekrarlanabilir kalır
        for (var i = 0; i < 200; i++)
        {
            var finalBalance = new Money(random.NextInt64(-5_000_000_000, 5_000_000_001));

            var distribution = MonthCalculator.Split(finalBalance, 0.90m);

            Assert.Equal(finalBalance.Satang, distribution.Partner1.Satang + distribution.Partner2.Satang);
        }
    }

    [Theory]
    [InlineData(15, 14, 1)]      // 15 × 0.90 = 13.5 → AwayFromZero → 14
    [InlineData(-15, -14, -1)]   // -13.5 → AwayFromZero → -14; toplam yine -15
    [InlineData(1, 1, 0)]        // 0.9 → 1
    [InlineData(-1, -1, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(128250, 115425, 12825)]
    public void Split_RoundsPartner1AwayFromZero_Partner2GetsRemainder(
        long balance, long expectedPartner1, long expectedPartner2)
    {
        var distribution = MonthCalculator.Split(new Money(balance), 0.90m);

        Assert.Equal(expectedPartner1, distribution.Partner1.Satang);
        Assert.Equal(expectedPartner2, distribution.Partner2.Satang);
    }

    [Fact]
    public void CalculateChain_SkipsEmptyDays_ButBalanceFlowsThrough()
    {
        var lines = new List<DatedTxnLine>
        {
            new(new DateOnly(2026, 7, 5), new TxnLine(TransactionType.Income, PaymentMethod.Cash, 100000)),
            // 6 ve 7 Temmuz boş: zincirde yer almaz ama bakiye aynen taşınır
            new(new DateOnly(2026, 7, 8), new TxnLine(TransactionType.Expense, PaymentMethod.Cash, 30000)),
        };

        var chain = MonthCalculator.CalculateChain(lines, posFeeRate: 0.035m);

        Assert.Equal(2, chain.Count);
        Assert.Equal(new DateOnly(2026, 7, 5), chain[0].Date);
        Assert.Equal(0, chain[0].PreviousBalance.Satang);
        Assert.Equal(100000, chain[0].Result.ClosingBalance.Satang);
        Assert.Equal(new DateOnly(2026, 7, 8), chain[1].Date);
        Assert.Equal(100000, chain[1].PreviousBalance.Satang);
        Assert.Equal(70000, chain[1].Result.ClosingBalance.Satang);
    }

    [Fact]
    public void CalculatePreviousBalance_NoPriorLines_ReturnsZero()
    {
        var previousBalance = MonthCalculator.CalculatePreviousBalance([], posFeeRate: 0.035m);

        Assert.Equal(0, previousBalance.Satang);
    }

    // ── Faz 7: ay toplamları (UI/Excel alt toplam satırının kaynağı) ───────────────────────

    [Fact]
    public void Totals_SumsChainFields_AndDayNetEqualsFinalBalance()
    {
        var lines = new List<DatedTxnLine>
        {
            new(new DateOnly(2026, 7, 5), new TxnLine(TransactionType.Income, PaymentMethod.Cash, 100000)),
            new(new DateOnly(2026, 7, 5), new TxnLine(TransactionType.Expense, PaymentMethod.Cash, 20000)),
            new(new DateOnly(2026, 7, 8), new TxnLine(TransactionType.Income, PaymentMethod.CreditCard, 50000)),
        };

        var chain = MonthCalculator.CalculateChain(lines, posFeeRate: 0.035m);
        var totals = MonthCalculator.Totals(chain);

        Assert.Equal(150000, totals.IncomeTotal.Satang);
        Assert.Equal(20000, totals.ExpenseTotal.Satang);
        Assert.Equal(1750, totals.PosFee.Satang); // 50000 × 0.035
        Assert.Equal(128250, totals.DayNet.Satang);
        // Zincir ayın 1'inde 0'dan başladığı için DayNet toplamı = ay sonu bakiye
        Assert.Equal(chain[^1].Result.ClosingBalance.Satang, totals.DayNet.Satang);
    }

    [Fact]
    public void Totals_EmptyChain_IsAllZero()
    {
        var totals = MonthCalculator.Totals([]);

        Assert.Equal(0, totals.IncomeTotal.Satang);
        Assert.Equal(0, totals.ExpenseTotal.Satang);
        Assert.Equal(0, totals.PosFee.Satang);
        Assert.Equal(0, totals.DayNet.Satang);
    }
}
