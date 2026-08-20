using QuerySeek.Services.Normalizing;

namespace QuerySeek.UnitTests;

[TestFixture]
public class DefaultNormalizerTests
{
    [TestCase("мёд", ExpectedResult = "МЕД")]
    [TestCase("МЁд", ExpectedResult = "МЕД")]
    [TestCase("garçon", ExpectedResult = "GARCON")]
    [TestCase("español", ExpectedResult = "ESPANOL")]
    [TestCase("rôle", ExpectedResult = "ROLE")]
    [TestCase("café", ExpectedResult = "CAFE")]
    [TestCase("München", ExpectedResult = "MUNCHEN")]
    [TestCase("MUNCHEN de partis.\\/$#() №#12,1 -01", ExpectedResult = "MUNCHEN DE PARTIS.\\/$#() №#12,1 -01")]
    [TestCase("الـْكِتَابُ", ExpectedResult = "الكتاب")]
    [TestCase("੪", ExpectedResult = "4")]
    [TestCase("۷۶", ExpectedResult = "76")]
    [TestCase("angel's", ExpectedResult = "ANGELS")]
    public string TestNormalize(string input)
        => DefaultNormalizer.Instance.Normalize(input);
}
