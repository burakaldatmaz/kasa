using Kasa.Domain;

namespace Kasa.Tests;

public class MoneyTests
{
    [Fact]
    public void Addition_And_Subtraction()
    {
        var a = new Money(150);
        var b = new Money(70);

        Assert.Equal(220, (a + b).Satang);
        Assert.Equal(80, (a - b).Satang);
        Assert.Equal(-70, (Money.Zero - b).Satang);
    }

    [Theory]
    [InlineData(100, 0.035, 4)]    // 3.5 → 4
    [InlineData(50, 0.05, 3)]      // 2.5 → 3 (banker's rounding 2 verirdi — AwayFromZero doğrulaması)
    [InlineData(150, 0.05, 8)]     // 7.5 → 8
    [InlineData(-50, 0.05, -3)]    // -2.5 → -3 (negatifte sıfırdan uzağa)
    [InlineData(350000, 0.035, 12250)] // golden gün pos fee
    public void Percent_Rounds_AwayFromZero_To_Satang(long satang, double rate, long expected)
    {
        Assert.Equal(expected, new Money(satang).Percent((decimal)rate).Satang);
    }

    [Fact]
    public void ToThb_Converts_Satang()
    {
        Assert.Equal(177.50m, new Money(17750).ToThb());
    }
}
