using QuerySeek.Services.Helpers;

namespace QuerySeek.UnitTests;

[TestFixture]
public class StackBitArrayTests
{
    [Test]
    public void SetGetTest()
    {
        StackBitArray array = new(stackalloc int[8]);

        array.Set(1, true);
        array.Set(16, true);
        array.Set(56, true);
        array.Set(117, true);
        array.Set(205, true);

        Assert.That(array.Get(1), Is.True);
        Assert.That(array.Get(16), Is.True);
        Assert.That(array.Get(56), Is.True);
        Assert.That(array.Get(117), Is.True);
        Assert.That(array.Get(205), Is.True);
        Assert.That(array.Get(204), Is.False);
        Assert.That(array.Get(5), Is.False);
        Assert.That(array.Get(19), Is.False);
    }
}
