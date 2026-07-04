using Kasa.Domain;

namespace Kasa.Tests;

/// <summary>
/// BahtText golden testleri: makbuzun "IN WORDS" satırı dizgiden bağımsız, saf fonksiyonla
/// doğrulanır. Thai kritik kuralları (เอ็ด/หนึ่ง ayrımı, ยี่สิบ, ล้าน, satang) ayrı ayrı sabitlenir.
/// </summary>
public class BahtTextTests
{
    // ── İngilizce ──────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(300000, "Three thousand baht only")]          // plan golden
    [InlineData(100, "One baht only")]
    [InlineData(2100, "Twenty-one baht only")]
    [InlineData(500000, "Five thousand baht only")]
    [InlineData(1000000, "Ten thousand baht only")]
    [InlineData(2000000000, "Twenty million baht only")]      // ล้าน katmanı (20.000.000 baht)
    [InlineData(12550, "One hundred twenty-five baht and fifty satang")] // satang kısmı
    [InlineData(50, "Fifty satang")]                          // 0.50 baht — baht yok
    [InlineData(1550000, "Fifteen thousand five hundred baht only")]
    public void ToEnglishWords_Golden(long satang, string expected) =>
        Assert.Equal(expected, BahtText.ToEnglishWords(satang));

    // ── Thai ───────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(300000, "สามพันบาทถ้วน")]                       // plan golden
    [InlineData(100, "หนึ่งบาทถ้วน")]                            // tek başına 1 → หนึ่ง (เอ็ด değil)
    [InlineData(2100, "ยี่สิบเอ็ดบาทถ้วน")]                       // 21 → ยี่สิบ + เอ็ด
    [InlineData(1100, "สิบเอ็ดบาทถ้วน")]                         // 11 → สิบเอ็ด
    [InlineData(1000, "สิบบาทถ้วน")]                             // 10 → สิบ (หนึ่งสิบ değil)
    [InlineData(2000000000, "ยี่สิบล้านบาทถ้วน")]                 // ล้าน katmanı
    [InlineData(100000, "หนึ่งพันบาทถ้วน")]
    [InlineData(12550, "หนึ่งร้อยยี่สิบห้าบาทห้าสิบสตางค์")]        // satang
    [InlineData(50, "ห้าสิบสตางค์")]                            // 0.50 baht
    [InlineData(10100, "หนึ่งร้อยเอ็ดบาทถ้วน")]                   // 101 → หนึ่งร้อยเอ็ด
    public void ToThaiWords_Golden(long satang, string expected) =>
        Assert.Equal(expected, BahtText.ToThaiWords(satang));

    // ── Sınır: 0 ve negatif hata verir (makbuz sıfır tutar kesmez) ──────────────────────────
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-300000)]
    public void NonPositive_Throws(long satang)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BahtText.ToEnglishWords(satang));
        Assert.Throws<ArgumentOutOfRangeException>(() => BahtText.ToThaiWords(satang));
    }
}
