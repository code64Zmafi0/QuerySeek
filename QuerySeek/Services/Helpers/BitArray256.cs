namespace QuerySeek.Services.Helpers;

/// <summary>
/// Аналог BitArray на стеке для уменьшения аллокаций
/// </summary>
public struct BitArray256
{
    private ulong _part0;
    private ulong _part1;
    private ulong _part2;
    private ulong _part3;

    /// <summary>
    /// Получает или устанавливает значение бита по указанному индексу (0-255).
    /// </summary>
    public bool this[int index]
    {
        get
        {
            if ((uint)index >= 256)
                throw new ArgumentOutOfRangeException(nameof(index));

            int fieldIndex = index >> 6; // Деление на 64
            int bitPosition = index & 63; // Остаток от деления на 64

            return fieldIndex switch
            {
                0 => (_part0 & (1UL << bitPosition)) != 0,
                1 => (_part1 & (1UL << bitPosition)) != 0,
                2 => (_part2 & (1UL << bitPosition)) != 0,
                3 => (_part3 & (1UL << bitPosition)) != 0,
                _ => false
            };
        }
        set
        {
            if ((uint)index >= 256)
                throw new ArgumentOutOfRangeException(nameof(index));

            int fieldIndex = index >> 6;
            int bitPosition = index & 63;

            if (value)
            {
                // Установка бита в 1
                switch (fieldIndex)
                {
                    case 0: _part0 |= (1UL << bitPosition); break;
                    case 1: _part1 |= (1UL << bitPosition); break;
                    case 2: _part2 |= (1UL << bitPosition); break;
                    case 3: _part3 |= (1UL << bitPosition); break;
                }
            }
            else
            {
                // Сброс бита в 0
                switch (fieldIndex)
                {
                    case 0: _part0 &= ~(1UL << bitPosition); break;
                    case 1: _part1 &= ~(1UL << bitPosition); break;
                    case 2: _part2 &= ~(1UL << bitPosition); break;
                    case 3: _part3 &= ~(1UL << bitPosition); break;
                }
            }
        }
    }
}
