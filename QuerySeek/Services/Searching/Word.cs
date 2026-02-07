using QuerySeek.Services.Extensions;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Слово из запроса интерпретированное в нграммы
/// </summary>
/// <param name="word"></param>
public class Word(string word) : IEquatable<Word>
{
    public readonly string QueryWord = word;

    public readonly int[] NGrammsHashes = SeekTools.GetNgrams(word);

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
        => NGrammsHashes.Length;
}
