namespace QuerySeek.Services.Helpers;

/// <summary>
/// Стековый BitArray для сохранения информации о совпавших типах. СОЗДАВАТЬ stackalloc int[8] для 256 значений
/// </summary>
public readonly ref struct StackBitArray
{
    private readonly Span<int> _storage;

    public StackBitArray(Span<int> storage)
    {
        _storage = storage;
    }

    public bool Get(byte index)
        => (_storage[index >> 5] & (1 << (index & 31))) != 0;

    public void Set(byte index, bool value)
    {
        int elementIndex = index >> 5;
        int bitMask = 1 << (index & 31);

        if (value)
        {
            _storage[elementIndex] |= bitMask;
        }
        else
        {
            _storage[elementIndex] &= ~bitMask;
        }
    }
}
