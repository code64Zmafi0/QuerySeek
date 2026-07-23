using QuerySeek.Services.Normalizing;

namespace QuerySeek.UnitTests;

[TestFixture]
public class DefaultNameTokenizerTests
{
    [TestCase("номер123", ExpectedResult = new string[] { "номер", "123" })]
    [TestCase("abh123", ExpectedResult = new string[] { "abh", "123" })]
    [TestCase("номер123такой", ExpectedResult = new string[] { "номер", "123", "такой" })]
    [TestCase("привет*медвед", ExpectedResult = new string[] { "привет", "медвед" })]
    [TestCase("привет[медвед]", ExpectedResult = new string[] { "привет", "медвед" })]
    [TestCase("привет [медвед]", ExpectedResult = new string[] { "привет", "медвед" })]
    [TestCase("привет медвед", ExpectedResult = new string[] { "привет", "медвед" })]
    [TestCase("هَذَا الـْكِتَابُ جَدِيدٌ", ExpectedResult = new string[] { "هَذَا", "الـْكِتَابُ", "جَدِيدٌ" })]
    [TestCase("aa,bar.col!d)o0(d{h#gh", ExpectedResult = new string[] { "aa", "bar", "col", "d", "o", "0", "d", "h", "gh"})]
    [TestCase("۷۶", ExpectedResult = new string[] { "۷۶" })]
    public string[] SplitTest(string input)
        => [.. DefaultNameTokenizer.Instance.Tokenize(input)];

    [TestCase("номер/123", "/", ExpectedResult = new string[] { "номер", "/", "123" })]
    public string[] TestSplitCustomValues(string input, string customChars)
    {
        DefaultNameTokenizer splitter = new(customChars);

        return [.. splitter.Tokenize(input)];
    }
}
