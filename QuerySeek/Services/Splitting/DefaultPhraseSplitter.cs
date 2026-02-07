using System.Text;

namespace QuerySeek.Services.Splitting;

public class DefaultPhraseSplitter : IPhraseSplitter
{
    public static readonly DefaultPhraseSplitter Instance = new();

    private static readonly char[] _splitChars =
    {
        ' ',
        '#',
        '№',
        ')',
        '(',
        '.',
        ',',
        '^',
        '\'',
        '-'
    };

    /// <summary>
    /// Производит разбиение строки на токены
    /// </summary>
    public string[] Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return value.Normalize(NormalizationForm.FormC)
                    .Split(_splitChars, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .SelectMany(TokenSplit)
                    .Take(250)
                    .ToArray();
    }

    private static IEnumerable<string> TokenSplit(string word)
    {
        foreach (var i in SearchAllDigitWordCombinations(word))
            yield return i.Length > 250 ? i[..250] : i;
    }

    private static IEnumerable<string> SearchAllDigitWordCombinations(string value)
    {
        var combIndex = GetWordDigitIndex(value);

        if (combIndex != -1)
        {
            combIndex++;
            yield return value[..combIndex];
            foreach (var i in SearchAllDigitWordCombinations(value[combIndex..])) yield return i;
        }
        else
        {
            yield return value;
        }
    }

    private static int GetWordDigitIndex(string value)
    {
        for (var i = 0; i < value.Length - 1; i++)
        {
            var currentSymbol = value[i];
            var nextSymbol = value[i + 1];

            if (!char.IsDigit(currentSymbol) && char.IsDigit(nextSymbol)) return i;
            if (char.IsDigit(currentSymbol) && !char.IsDigit(nextSymbol)) return i;

            if (char.IsLetterOrDigit(currentSymbol) && char.IsPunctuation(nextSymbol)) return i;
            if (char.IsPunctuation(currentSymbol) && char.IsLetterOrDigit(nextSymbol)) return i;
        }

        return -1;
    }
}
