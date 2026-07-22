using QuerySeek.Services.Helpers;

namespace QuerySeek.UnitTests;

[TestFixture]
public class NgrammsTests
{
    [TestCase("WORD", ExpectedResult = new int[] { 816986859, -278555420, 77678514, -1038066975, -1132174916, 816989850 })]
    public int[] TestBuildNgramms(string word)
        => NgrammsWordsSearchHelper.GetNgramms(word);
}
