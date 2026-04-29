using Koovra.Cto.Core;
using Xunit;

namespace CtoAutocadAddin.Tests
{
    public class HpDistributorTests
    {
        [Theory]
        [InlineData(9,  2, new[] { 5, 4 })]
        [InlineData(10, 3, new[] { 4, 3, 3 })]
        [InlineData(0,  2, new[] { 0, 0 })]
        [InlineData(3,  3, new[] { 1, 1, 1 })]
        [InlineData(5,  1, new[] { 5 })]
        public void Distribute_ReturnsCorrectSplit(int hp, int n, int[] expected)
        {
            Assert.Equal(expected, HpDistributor.Distribute(hp, n));
        }

        [Fact]
        public void Distribute_ZeroCajas_ReturnsEmpty()
        {
            Assert.Empty(HpDistributor.Distribute(9, 0));
        }
    }
}
