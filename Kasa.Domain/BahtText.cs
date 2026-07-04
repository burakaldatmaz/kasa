using System.Globalization;
using System.Text;

namespace Kasa.Domain;

/// <summary>
/// Tutarın (satang) harflerle yazımı — depozito makbuzunun "IN WORDS / ตัวอักษร" satırı.
/// Saf fonksiyon: girdi satang (I1), çıktı metin; hiçbir toplama/yuvarlama yapılmaz.
/// Baht kısmı tam sayı olarak, satang kısmı (varsa) ayrı okunur.
/// Thai kuralları: birler basamağı 1 → เอ็ด (11 = สิบเอ็ด, 21 = ยี่สิบเอ็ด) ama tek başına 1 → หนึ่ง;
/// onlar basamağı 1 → สิบ, 2 → ยี่สิบ; milyon katmanı ล้าน. Tam baht → "...บาทถ้วน",
/// satang varsa → "...บาท...สตางค์".
/// </summary>
public static class BahtText
{
    private const string NotPositive = "Tutar sıfırdan büyük olmalıdır (satang).";

    private static readonly string[] ThaiDigit =
        ["ศูนย์", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า"];

    // Grup içi basamak adları: birler("") · สิบ · ร้อย · พัน · หมื่น · แสน (index = sağdan konum).
    private static readonly string[] ThaiPlace = ["", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน"];

    private static readonly string[] EngOnes =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
        "seventeen", "eighteen", "nineteen"
    ];

    private static readonly string[] EngTens =
        ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>satang → Thai makbuz metni (örn: 300000 → "สามพันบาทถ้วน", 12550 → "หนึ่งร้อยยี่สิบห้าบาทห้าสิบสตางค์").</summary>
    public static string ToThaiWords(long satang)
    {
        var (baht, sat) = Split(satang);
        var sb = new StringBuilder();

        if (baht > 0)
            sb.Append(IntegerToThai(baht)).Append("บาท");

        if (sat == 0)
            sb.Append("ถ้วน"); // baht>0 garanti (satang>0 & sat==0 ⇒ baht>0)
        else
            sb.Append(IntegerToThai(sat)).Append("สตางค์");

        return sb.ToString();
    }

    /// <summary>satang → İngilizce makbuz metni (örn: 300000 → "Three thousand baht only", 12550 → "One hundred twenty-five baht and fifty satang").</summary>
    public static string ToEnglishWords(long satang)
    {
        var (baht, sat) = Split(satang);
        var sb = new StringBuilder();

        if (baht > 0)
            sb.Append(IntegerToEnglish(baht)).Append(" baht");

        if (sat == 0)
        {
            if (baht > 0)
                sb.Append(" only");
        }
        else
        {
            if (baht > 0)
                sb.Append(" and ");
            sb.Append(IntegerToEnglish(sat)).Append(" satang");
        }

        var result = sb.ToString();
        return string.Concat(char.ToUpperInvariant(result[0]).ToString(), result.AsSpan(1));
    }

    /// <param name="satang">Pozitif tutar; 0/negatif geçersizdir (makbuz sıfır tutar kesmez).</param>
    private static (long Baht, int Satang) Split(long satang)
    {
        if (satang <= 0)
            throw new ArgumentOutOfRangeException(nameof(satang), satang, NotPositive);
        return (satang / 100, (int)(satang % 100));
    }

    /// <summary>Tam sayı → Thai okunuşu. 6'lı gruplara bölünür, gruplar ล้าน ile birleşir.</summary>
    private static string IntegerToThai(long n)
    {
        if (n == 0)
            return ThaiDigit[0];

        var groups = new List<int>();
        while (n > 0)
        {
            groups.Add((int)(n % 1_000_000));
            n /= 1_000_000;
        }

        var sb = new StringBuilder();
        for (var gi = groups.Count - 1; gi >= 0; gi--)
        {
            if (groups[gi] == 0)
                continue;

            sb.Append(ReadSixDigits(groups[gi]));
            for (var m = 0; m < gi; m++)
                sb.Append("ล้าน");
        }

        return sb.ToString();
    }

    /// <summary>1..999999 arası bir grubu okur (basamak kuralları: สิบ/ยี่สิบ, birler 1 → เอ็ด).</summary>
    private static string ReadSixDigits(int num)
    {
        var s = num.ToString(CultureInfo.InvariantCulture);
        var len = s.Length;
        var sb = new StringBuilder();

        for (var i = 0; i < len; i++)
        {
            var digit = s[i] - '0';
            if (digit == 0)
                continue;

            var place = len - i - 1; // sağdan konum: 0 birler .. 5 แสน

            if (place == 0 && digit == 1 && len > 1)
                sb.Append("เอ็ด"); // ToString'de baştaki sıfır yok ⇒ len>1 ⇔ üstte sıfırdan farklı basamak var
            else if (place == 1 && digit == 1)
                sb.Append("สิบ");
            else if (place == 1 && digit == 2)
                sb.Append("ยี่สิบ");
            else
                sb.Append(ThaiDigit[digit]).Append(ThaiPlace[place]);
        }

        return sb.ToString();
    }

    /// <summary>Tam sayı → İngilizce okunuşu (kısa ölçek: thousand/million/billion/trillion).</summary>
    private static string IntegerToEnglish(long n)
    {
        if (n < 20)
            return EngOnes[n];

        if (n < 100)
        {
            var tens = EngTens[n / 10];
            return n % 10 == 0 ? tens : $"{tens}-{EngOnes[n % 10]}";
        }

        if (n < 1000)
        {
            var hundreds = $"{EngOnes[n / 100]} hundred";
            return n % 100 == 0 ? hundreds : $"{hundreds} {IntegerToEnglish(n % 100)}";
        }

        var (div, name) = n switch
        {
            >= 1_000_000_000_000L => (1_000_000_000_000L, "trillion"),
            >= 1_000_000_000L => (1_000_000_000L, "billion"),
            >= 1_000_000L => (1_000_000L, "million"),
            _ => (1000L, "thousand")
        };

        var head = $"{IntegerToEnglish(n / div)} {name}";
        return n % div == 0 ? head : $"{head} {IntegerToEnglish(n % div)}";
    }
}
