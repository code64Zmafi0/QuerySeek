using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching.Models;

/// <summary>
/// Контейнер слова из запроса с альтернативами и множителем
/// </summary>
/// <param name="QueryWord"></param>
/// <param name="Alternatives"></param>
/// <param name="Multipler"></param>
public record QueryWordContainer(Word QueryWord, Word[] Alternatives, double Multipler);

/// <summary>
/// Слово из запроса интерпретированное в нграммы
/// </summary>
/// <param name="word"></param>
public class Word(string word) : IEquatable<Word>
{
    public readonly string QueryWord = word;

    public readonly int[] NGrammsHashes = NgrammsHelper.GetNgramms(word);

    public readonly bool IsDigit = int.TryParse(word, out _);

    public bool Equals(Word? other)
    {
        if (other is null) return false;

        return other.QueryWord.Equals(QueryWord);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Word w)
            return false;

        return Equals(w);
    }

    public override int GetHashCode()
        => QueryWord.GetHashCode();
}
