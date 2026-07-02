using Kasa.Domain;

namespace Kasa.Tests;

public class FleetCalculatorTests
{
    [Theory]
    [InlineData(59, 4, 44, "80.0")]  // golden filo: 44/55 tam %80.0
    [InlineData(16, 0, 1, "6.3")]    // 6.25 → AwayFromZero 6.3 (banker's ToEven 6.2 verirdi)
    [InlineData(3, 0, 1, "33.3")]    // 1/3 filo: 33.33... → 33.3
    [InlineData(3, 0, 2, "66.7")]    // 66.66... yukarı yuvarlanır
    [InlineData(10, 2, 8, "100.0")]  // kiralanabilirlerin tamamı kirada
    [InlineData(10, 3, 0, "0.0")]
    public void RentalPercent_RoundsAwayFromZeroToOneDecimal(
        int total, int broken, int rented, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            FleetCalculator.RentalPercent(total, broken, rented));
    }

    [Theory]
    [InlineData(5, 5, 0)]  // tüm filo arızalı
    [InlineData(0, 0, 0)]  // hiç bisiklet yok
    public void RentalPercent_NoAvailableBikes_ReturnsNull(int total, int broken, int rented)
    {
        Assert.Null(FleetCalculator.RentalPercent(total, broken, rented));
    }

    [Fact]
    public void IdleBikes_IsTotalMinusBrokenMinusRented()
    {
        Assert.Equal(11, FleetCalculator.IdleBikes(59, 4, 44));
        Assert.Equal(0, FleetCalculator.IdleBikes(5, 5, 0));
    }

    [Fact]
    public void AverageRentalPercent_ExcludesNullDays()
    {
        // null gün ortalamaya katılmaz: (50.0 + 75.0) / 2 = 62.5
        Assert.Equal(62.5m, FleetCalculator.AverageRentalPercent([50.0m, null, 75.0m]));
    }

    [Fact]
    public void AverageRentalPercent_NoValues_ReturnsNull()
    {
        Assert.Null(FleetCalculator.AverageRentalPercent([]));
        Assert.Null(FleetCalculator.AverageRentalPercent([null, null]));
    }

    [Fact]
    public void AverageRentalPercent_RoundsAwayFromZero()
    {
        // (33.3 + 33.2) / 2 = 33.25 → AwayFromZero 33.3
        Assert.Equal(33.3m, FleetCalculator.AverageRentalPercent([33.3m, 33.2m]));
    }
}
