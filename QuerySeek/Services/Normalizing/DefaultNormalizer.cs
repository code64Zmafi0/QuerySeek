using System.Buffers;
using System.Globalization;
using System.Text;

namespace QuerySeek.Services.Normalizing;

public class DefaultNormalizer : INormalizer
{
    public static readonly DefaultNormalizer Instance = new();

    private static readonly ArrayPool<char> _pool = ArrayPool<char>.Shared;

    public string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // 1. Декомпозиция (FormD) разделяет базовые символы и их акценты.
        string normalizedString = value.Normalize(NormalizationForm.FormD);

        int maxLength = normalizedString.Length;
        char[]? rentedArray = null;

        // Используем stackalloc для коротких строк (до 512 символов), 
        // чтобы вообще не трогать кучу. Для длинных - берем из пула.
        Span<char> destination = maxLength <= 512
            ? stackalloc char[maxLength]
            : (rentedArray = _pool.Rent(maxLength));

        try
        {
            int pointer = 0;

            foreach (char c in normalizedString)
            {
                double numericValue = char.GetNumericValue(c);

                if (numericValue >= 0 && numericValue <= 9)
                {
                    destination[pointer++] = (char)('0' + (int)numericValue);
                    continue;
                }

                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);

                if (category == UnicodeCategory.UppercaseLetter ||
                    category == UnicodeCategory.LowercaseLetter ||
                    category == UnicodeCategory.DecimalDigitNumber ||
                    category == UnicodeCategory.OtherLetter ||
                    category == UnicodeCategory.OtherPunctuation ||
                    category == UnicodeCategory.SpaceSeparator)
                {
                    destination[pointer++] = char.ToUpperInvariant(c);
                }
            }

            return pointer <= 0
                ? string.Empty
                : new string(destination[0..pointer].Trim());
        }
        finally
        {
            if (rentedArray != null)
                _pool.Return(rentedArray);
        }
    }
}
