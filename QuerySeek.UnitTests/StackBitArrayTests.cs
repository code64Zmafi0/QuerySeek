using QuerySeek.Services.Helpers;

namespace QuerySeek.UnitTests;

[TestFixture]
public class StackBitArrayTests
{
    [Test]
    public void SetGetTest()
    {
        BitArray256 array = new();

        array[1] = true;
        array[16] = true;
        array[56] = true;
        array[117] = true;
        array[205] = true;

        Assert.That(array[1], Is.True);
        Assert.That(array[16], Is.True);
        Assert.That(array[56], Is.True);
        Assert.That(array[117], Is.True);
        Assert.That(array[205], Is.True);
        Assert.That(array[204], Is.False);
        Assert.That(array[5], Is.False);
        Assert.That(array[19], Is.False);
    }
}
