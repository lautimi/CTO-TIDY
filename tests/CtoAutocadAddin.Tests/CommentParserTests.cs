using Koovra.Cto.Core;
using Xunit;

namespace Koovra.Cto.Tests
{
    public class CommentParserTests
    {
        [Theory]
        [InlineData("HP=12", 12)]
        [InlineData("hp:20", 20)]
        [InlineData("HP 3", 3)]
        [InlineData("poste con HP=8 y VEG", 8)]
        [InlineData("sin numero", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void TryExtractHp_Works(string input, int? expected)
        {
            int? r = CommentParser.TryExtractHp(input);
            Assert.Equal(expected, r);
        }

        [Theory]
        [InlineData("VEG y FDR", "VEG", "FDR")]
        [InlineData("APOYO inclinado", "APOYO", "INCLINADO")]
        [InlineData("sin codigos", null, null)]
        public void ExtractKnownCodes_Works(string input, string expected1, string expected2)
        {
            var codes = System.Linq.Enumerable.ToList(CommentParser.ExtractKnownCodes(input));
            if (expected1 != null) Assert.Contains(expected1, codes);
            if (expected2 != null) Assert.Contains(expected2, codes);
        }

        [Fact]
        public void JoinCsv_DeduplicatesAndTrims()
        {
            string csv = CommentParser.JoinCsv(new[] { "VEG", "veg", " APOYO ", "APOYO" });
            Assert.Equal("VEG;APOYO", csv);
        }
    }
}
