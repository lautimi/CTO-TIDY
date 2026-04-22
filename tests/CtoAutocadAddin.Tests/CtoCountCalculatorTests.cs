using Koovra.Cto.Core;
using Xunit;

namespace Koovra.Cto.Tests
{
    public class CtoCountCalculatorTests
    {
        [Theory]
        // Formato: (hp, largo, cDespEsperado, cCrecEsperado)
        // Ejes ≤ 160 m
        [InlineData(0,   100.0, 0, 0)]
        [InlineData(1,   100.0, 0, 1)]
        [InlineData(2,   100.0, 0, 1)]
        [InlineData(3,   100.0, 1, 0)]
        [InlineData(5,   100.0, 1, 0)]
        [InlineData(6,   100.0, 1, 0)]
        [InlineData(8,   100.0, 1, 0)]
        [InlineData(9,   100.0, 1, 1)]
        [InlineData(16,  100.0, 1, 1)]
        [InlineData(17,  100.0, 1, 2)]
        [InlineData(20,  100.0, 1, 2)]
        [InlineData(21,  100.0, 2, 1)]
        [InlineData(24,  100.0, 2, 1)]
        [InlineData(25,  100.0, 2, 2)]
        [InlineData(32,  100.0, 2, 2)]
        [InlineData(33,  100.0, 2, 3)]
        [InlineData(40,  100.0, 2, 3)]
        [InlineData(41,  100.0, 3, 3)]
        [InlineData(48,  100.0, 3, 3)]
        [InlineData(49,  100.0, 3, 4)]
        [InlineData(56,  100.0, 3, 4)]
        [InlineData(57,  100.0, 4, 4)]
        [InlineData(64,  100.0, 4, 4)]
        // Corte exacto en 160 m (≤ 160 cae en columna corta)
        [InlineData(20,  160.0, 1, 2)]
        // Ejes > 160 m
        [InlineData(1,   200.0, 0, 0)]
        [InlineData(2,   200.0, 0, 0)]
        [InlineData(3,   200.0, 1, 0)]
        [InlineData(5,   200.0, 1, 0)]
        [InlineData(6,   200.0, 2, 0)]
        [InlineData(8,   200.0, 2, 0)]
        [InlineData(9,   200.0, 2, 0)]
        [InlineData(16,  200.0, 2, 0)]
        [InlineData(17,  200.0, 2, 1)]
        [InlineData(20,  200.0, 2, 1)]
        [InlineData(21,  200.0, 2, 1)]
        [InlineData(24,  200.0, 2, 1)]
        [InlineData(25,  200.0, 2, 2)]
        [InlineData(32,  200.0, 2, 2)]
        [InlineData(33,  200.0, 2, 3)]
        [InlineData(40,  200.0, 2, 3)]
        [InlineData(41,  200.0, 3, 3)]
        [InlineData(48,  200.0, 3, 3)]
        [InlineData(49,  200.0, 3, 4)]
        [InlineData(56,  200.0, 3, 4)]
        [InlineData(57,  200.0, 4, 4)]
        [InlineData(64,  200.0, 4, 4)]
        // Fuera de rango: aplica el último rango (57-64)
        [InlineData(100, 100.0, 4, 4)]
        [InlineData(200, 300.0, 4, 4)]
        public void Calculate_MatchesOfficialTable(int hp, double largo, int expectedCDesp, int expectedCCrec)
        {
            var result = CtoCountCalculator.Calculate(hp, largo);

            Assert.Equal(expectedCDesp, result.CDesp);
            Assert.Equal(expectedCCrec, result.CCrec);
            Assert.Equal(expectedCDesp + expectedCCrec, result.Total);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(64, false)]
        [InlineData(65, true)]
        [InlineData(1000, true)]
        public void IsOutOfRange_ReturnsExpected(int hp, bool expected)
        {
            Assert.Equal(expected, CtoCountCalculator.IsOutOfRange(hp));
        }

        [Fact]
        public void Calculate_NegativeHp_ReturnsZero()
        {
            var r = CtoCountCalculator.Calculate(-5, 100.0);
            Assert.Equal(0, r.Total);
        }
    }
}
