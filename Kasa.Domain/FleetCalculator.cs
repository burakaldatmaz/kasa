namespace Kasa.Domain;

/// <summary>
/// Günlük filo hesaplayıcısı. Saf ve durumsuz (I2): tüm filo matematiği burada, API'de hesap yok.
/// </summary>
public static class FleetCalculator
{
    /// <summary>
    /// Kiralama yüzdesi: rented / (total − broken) × 100, 1 ondalığa AwayFromZero yuvarlı.
    /// Kiralanabilir bisiklet yoksa (total − broken ≤ 0) null döner — sıfıra bölme yok.
    /// </summary>
    public static decimal? RentalPercent(int totalBikes, int brokenBikes, int rentedBikes)
    {
        var available = totalBikes - brokenBikes;
        if (available <= 0)
            return null;

        // 100.0m: bölüm sonucu en az 1 ondalık scale taşır → JSON'da 80 değil 80.0 yazılır
        return Math.Round(rentedBikes * 100.0m / available, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Boşta bekleyen bisiklet: total − broken − rented.</summary>
    public static int IdleBikes(int totalBikes, int brokenBikes, int rentedBikes) =>
        totalBikes - brokenBikes - rentedBikes;

    /// <summary>
    /// Günlük yüzdelerin ortalaması, 1 ondalığa AwayFromZero yuvarlı.
    /// null günler (kiralanabilir bisikleti olmayan) ortalamaya katılmaz; hiç değer yoksa null.
    /// </summary>
    public static decimal? AverageRentalPercent(IEnumerable<decimal?> dailyPercents)
    {
        var values = dailyPercents.Where(p => p.HasValue).Select(p => p!.Value).ToList();
        if (values.Count == 0)
            return null;

        return Math.Round(values.Average(), 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Rezervasyon sayacı ay toplamı (Faz 11): null günler ("girilmedi") toplama katılmaz;
    /// TÜM günler null ise toplam da null — 0 "gerçekten sıfır" anlamını korur (K2).
    /// </summary>
    public static int? SumCounters(IEnumerable<int?> dailyCounts)
    {
        var values = dailyCounts.Where(c => c.HasValue).Select(c => c!.Value).ToList();
        return values.Count == 0 ? null : values.Sum();
    }
}
