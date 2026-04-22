using Koovra.Cto.Core;
using Xunit;

namespace Koovra.Cto.Tests
{
    public class AddressMatcherTests
    {
        [Theory]
        [InlineData("San Martin 1250", "San Martin", "OK")]
        [InlineData("AV San Martin 250", "san martin", "OK")]
        [InlineData("Rivadavia 500", "San Martin", "REVISAR")]
        [InlineData("", "San Martin", "SIN_CALLE_POSTE")]
        [InlineData(null, "San Martin", "SIN_CALLE_POSTE")]
        [InlineData("San Martin 1250", "", "REVISAR")]
        public void Compare_Works(string dirPoste, string dirSegmento, string expected)
        {
            Assert.Equal(expected, AddressMatcher.Compare(dirPoste, dirSegmento));
        }
    }
}
