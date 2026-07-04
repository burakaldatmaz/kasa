using Kasa.Domain;

namespace Kasa.Tests;

public class DepositNumberTests
{
    [Theory]
    [InlineData(2026, 418, "DEP-2026-00418")]  // plan golden
    [InlineData(2026, 1, "DEP-2026-00001")]
    [InlineData(2026, 99999, "DEP-2026-99999")]
    [InlineData(2027, 1, "DEP-2027-00001")]    // yıl sınırında sıfırlanır
    public void Format_PadsYearAndSequence(int year, int seq, string expected) =>
        Assert.Equal(expected, DepositNumber.Format(year, seq));
}
