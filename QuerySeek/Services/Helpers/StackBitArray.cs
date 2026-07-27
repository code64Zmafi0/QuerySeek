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

    /// <summary>
    /// Возвращает совпавшие типы
    /// </summary>
    /// <returns></returns>
    public List<int> GetTrueIndices()
    {
        List<int> result = [];

        for (byte i = 0; i < 4; i++)
        {
            int currentInt = _storage[i];

            if (currentInt == 0) continue;

            int baseIndex = i << 5;

            for (int bit = 0; bit < 32; bit++)
            {
                if ((currentInt & (1 << bit)) != 0)
                {
                    result.Add(baseIndex + bit);
                }
            }
        }

        return result;
    }
}
