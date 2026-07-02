namespace Kasa.Domain;

/// <summary>
/// Para değeri. Integer satang olarak saklanır (1 THB = 100 satang).
/// Tüm para hesapları server-side ve bu tip üzerinden yapılır (I1).
/// </summary>
public readonly record struct Money(long Satang)
{
    public static readonly Money Zero = new(0);

    public static Money operator +(Money a, Money b) => new(checked(a.Satang + b.Satang));

    public static Money operator -(Money a, Money b) => new(checked(a.Satang - b.Satang));

    /// <summary>
    /// Yüzde hesabı: tutar × oran, satang'a MidpointRounding.AwayFromZero ile yuvarlanır.
    /// </summary>
    public Money Percent(decimal rate) =>
        new((long)Math.Round(Satang * rate, 0, MidpointRounding.AwayFromZero));

    public decimal ToThb() => Satang / 100m;

    public override string ToString() => $"{ToThb():0.00} THB";
}
