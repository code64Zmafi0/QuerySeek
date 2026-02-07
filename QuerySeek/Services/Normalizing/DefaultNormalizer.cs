using System.Text;

namespace QuerySeek.Services.Normalizing;

public class DefaultNormalizer : INormalizer
{
    public static readonly DefaultNormalizer Instance = new();

    private static readonly List<(string Find, string Replace)> _replaces =
    [
        ("Й", "И"),
        ("Ё", "Е"),
        (".", " "),
        ("'", " "),
        (",", " "),
        ("№", " "),
        ("-", " "),
        ("(", " "),
        (")", " "),
        ("[", " "),
        ("]", " "),
        ("{", " "),
        ("}", " "),
        ("ç", "C"),
        ("ə", "E"),
        ("ğ", "G"),
        ("ı", "I"),
        ("ş", "S"),
        ("ü", "U"),
        ("Ç", "C"),
        ("Ə", "E"),
        ("Ğ", "G"),
        ("İ", "I"),
        ("Ö", "O"),
        ("Ş", "S"),
        ("Ü", "U"),
        ("í", "I"),
        ("ó", "O"),
        ("ç", "C"),
        ("ã", "A"),
        ("á", "A"),
        ("é", "E"),
    ];

    private const char SPACE_CHAR = ' ';

    /// <summary>
    /// Данная функция оптимальным образом делает замены без создания тысячи строк
    /// </summary>
    /// <param name="phrase"></param>
    /// <returns></returns>
    public string Normalize(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return string.Empty;

        var resultBuilder = new StringBuilder();
        var lastWasSpace = false;
        for (var i = 0; i < phrase.Length;)
        {
            if (resultBuilder.Length == 0 && phrase[i] == SPACE_CHAR)
            {
                i++;
                continue;
            }

            var replaced = false;
            for (var j = 0; j < _replaces.Count; j++)
            {
                var r = _replaces[j];
                if (phrase.IndexOf(r.Find, i, StringComparison.OrdinalIgnoreCase) == i)
                {
                    i += r.Find.Length;
                    if (r.Replace.Length == 1 && r.Replace[0] != SPACE_CHAR || !lastWasSpace) resultBuilder.Append(r.Replace);
                    replaced = true;
                }
            }

            if (replaced) continue;

            lastWasSpace = phrase[i] == SPACE_CHAR;
            resultBuilder.Append(char.ToUpperInvariant(phrase[i]));
            i++;
        }

        var indexOfLastSpace = resultBuilder.Length - 1;
        while (indexOfLastSpace >= 0 && resultBuilder[indexOfLastSpace] == ' ')
        {
            indexOfLastSpace--;
        }

        return resultBuilder.ToString(0, indexOfLastSpace + 1);
    }
}
