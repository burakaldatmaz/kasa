using System.Globalization;
using System.Text.RegularExpressions;

namespace Kasa.Api;

/// <summary>
/// Frontend'den BAHT string gelir, server satang'a çevirir (I1 — para hesabı sadece burada).
/// Kabul: nokta ayraçlı, en fazla 2 ondalık, sıfırdan büyük. Örn: "2300" → 230000, "2300.5" → 230050.
/// </summary>
public static partial class BahtParser
{
    public const string FormatError =
        "Geçersiz tutar. Nokta ayraçlı, en fazla 2 ondalıklı bir sayı girin (örn: 2300 veya 2300.50).";

    public const string NotPositiveError = "Tutar sıfırdan büyük olmalıdır.";

    [GeneratedRegex(@"^\d{1,15}(\.\d{1,2})?$")]
    private static partial Regex BahtPattern();

    public static bool TryParse(string? input, out long satang, out string error)
    {
        satang = 0;
        error = FormatError;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var s = input.Trim();
        if (!BahtPattern().IsMatch(s))
            return false;

        var parts = s.Split('.');
        var baht = long.Parse(parts[0], CultureInfo.InvariantCulture);
        var fraction = parts.Length == 2
            ? long.Parse(parts[1].PadRight(2, '0'), CultureInfo.InvariantCulture)
            : 0;

        var result = baht * 100 + fraction;
        if (result <= 0)
        {
            error = NotPositiveError;
            return false;
        }

        satang = result;
        error = "";
        return true;
    }
}
