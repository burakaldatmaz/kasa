namespace Kasa.Domain;

/// <summary>
/// Ay bazlı saf hesaplayıcı (I2): devir zinciri, kategori toplamları ve ortak dağıtımı.
/// DailyCalculator gibi durumsuzdur; EF/HTTP bağımlılığı yoktur.
/// </summary>
public static class MonthCalculator
{
    /// <summary>
    /// Aynı aya ait işlem satırlarından gün-gün devir zinciri kurar.
    /// Zincir ayın 1'inde 0'dan başlar; her günün deviri önceki günlerin DayNet toplamıdır.
    /// Devir saklanmaz, her çağrıda satırlardan yeniden hesaplanır — geçmiş güne düzeltme
    /// girilirse sonraki tüm günlerin deviri otomatik doğru olur.
    /// Sadece işlem olan günler döner; aradaki boş günler bakiyeyi aynen taşır.
    /// </summary>
    public static IReadOnlyList<ChainDay> CalculateChain(IEnumerable<DatedTxnLine> lines, decimal posFeeRate)
    {
        var days = new List<ChainDay>();
        var balance = Money.Zero;

        foreach (var group in lines.GroupBy(l => l.Date).OrderBy(g => g.Key))
        {
            var result = DailyCalculator.Calculate(
                [.. group.Select(l => l.Line)], posFeeRate, balance.Satang);
            days.Add(new ChainDay(group.Key, balance, result));
            balance = result.ClosingBalance;
        }

        return days;
    }

    /// <summary>
    /// Bir günün deviri: aynı ayda o günden ÖNCEKİ satırların zincir sonu bakiyesi.
    /// Önceki gün yoksa (ayın 1'i veya ay başından beri işlemsiz) 0 döner.
    /// </summary>
    public static Money CalculatePreviousBalance(IEnumerable<DatedTxnLine> priorLines, decimal posFeeRate)
    {
        var chain = CalculateChain(priorLines, posFeeRate);
        return chain.Count == 0 ? Money.Zero : chain[^1].Result.ClosingBalance;
    }

    /// <summary>
    /// Zincir günlerinin ay toplamları. Alt toplam satırı (UI ve Excel) hesabı burada yapılır;
    /// istemciler yalnızca gösterir (I1).
    /// </summary>
    public static MonthTotals Totals(IEnumerable<ChainDay> chain) =>
        chain.Aggregate(
            new MonthTotals(Money.Zero, Money.Zero, Money.Zero, Money.Zero),
            (t, d) => new MonthTotals(
                t.IncomeTotal + d.Result.IncomeTotal,
                t.ExpenseTotal + d.Result.ExpenseTotal,
                t.PosFee + d.Result.PosFee,
                t.DayNet + d.Result.DayNet));

    /// <summary>Kategori toplamları. Kategoriler girişteki ilk görülme sırasıyla döner.</summary>
    public static IReadOnlyList<CategoryTotal> SumByCategory(IEnumerable<CategoryAmount> items) =>
        [.. items
            .GroupBy(i => i.Category)
            .Select(g => new CategoryTotal(
                g.Key,
                g.Aggregate(Money.Zero, (total, i) => total + new Money(i.AmountSatang))))];

    /// <summary>
    /// Ay sonu bakiyesinin ortaklara dağıtımı:
    ///   partner1 = bakiye × pay (AwayFromZero, satang'a yuvarlı)
    ///   partner2 = bakiye − partner1
    /// Toplam DAİMA bakiyeye eşittir, 1 satang kaybolmaz. Negatif bakiyede de aynı formül geçerlidir.
    /// </summary>
    public static Distribution Split(Money finalBalance, decimal partner1Share)
    {
        var partner1 = finalBalance.Percent(partner1Share);
        return new Distribution(partner1, finalBalance - partner1);
    }
}
