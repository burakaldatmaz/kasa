using System.Globalization;

namespace Kasa.Domain;

/// <summary>
/// Depozito makbuz numarasının biçimi: "DEP-YYYY-NNNNN" (yıl 4 hane, sıra 5 hane sıfır dolgulu).
/// Yalnızca biçimlendirme; tekrarsız sıra üretimi (yıl bazlı MAX+1) API katmanının işidir.
/// </summary>
public static class DepositNumber
{
    /// <param name="year">Makbuz yılı (örn. 2026).</param>
    /// <param name="seq">Yıl içindeki sıra (1'den başlar, örn. 418 → "00418").</param>
    public static string Format(int year, int seq) =>
        string.Create(CultureInfo.InvariantCulture, $"DEP-{year:0000}-{seq:00000}");
}
