using NUnit.Framework;

namespace TpsDungeon.Progression.Tests
{
    /// <summary>大きな数の短い表示（ShortNumber）。</summary>
    public sealed class ShortNumberTests
    {
        [TestCase(0L, "0")]
        [TestCase(950L, "950")]
        [TestCase(999L, "999")]
        [TestCase(1_000L, "1K")]
        [TestCase(2_000L, "2K")]
        [TestCase(1_234L, "1.23K")]
        [TestCase(1_050L, "1.05K")]
        [TestCase(1_500L, "1.5K")]
        [TestCase(12_345L, "12.3K")]
        [TestCase(25_000L, "25K")]
        [TestCase(123_456L, "123K")]
        [TestCase(1_600_000L, "1.6M")]
        [TestCase(62_500_000L, "62.5M")]
        [TestCase(3_000_000_000L, "3B")]
        [TestCase(1_000_000_000_000L, "1T")]
        [TestCase(-2_500L, "-2.5K")]
        public void Format_ShortensWithSuffix(long value, string expected)
        {
            Assert.AreEqual(expected, ShortNumber.Format(value));
        }

        [TestCase(999_999L, "999K", Description = "切り捨てるので 1M にはならない")]
        [TestCase(1_999L, "1.99K")]
        [TestCase(99_999_999L, "99.9M")]
        public void Format_TruncatesInsteadOfRounding(long value, string expected)
        {
            Assert.AreEqual(expected, ShortNumber.Format(value));
        }
    }
}
