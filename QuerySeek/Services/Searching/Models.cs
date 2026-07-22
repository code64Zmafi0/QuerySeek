using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching;

public class TypeSearchResult(byte type, EntitySearchResult[] result)
{
    public readonly byte Type = type;

    public readonly EntitySearchResult[] Result = result;
}

public class EntitySearchResult(Key key, EntityMeta meta)
{
    public readonly Key Key = key;

    public readonly EntityMeta Meta = meta;

    public readonly List<WordCompareResult> WordsMatches = new(1);

    public readonly List<AdditionalRule> Rules = [];

    public int Prescore;

    public int Score;

    public Key[] GetLinks()
        => Meta.Links;

    public Key[] GetChilds()
        => Meta.Childs;

    public void AddRule(AdditionalRule rule)
        => Rules.Add(rule);

    internal void AddMatch(WordCompareResult wordCompareResult)
    {
        WordsMatches.Add(wordCompareResult);
        Prescore += wordCompareResult.MatchLength;
    }
}

/// <summary>
/// Описывает совпавщее слово в сущности
/// </summary>
/// <param name="NameWordPosition">Позиция совпавшего слова в имени</param>
/// <param name="PhraseType">Тип фразы</param>
/// <param name="QueryWordPosition">Позиция совпавшего слова из запроса</param>
/// <param name="MatchLength">Длина совпадения (по количеству свопавщих нграмм)</param>
public readonly record struct WordCompareResult(
    byte NameWordPosition,
    byte PhraseType,
    byte QueryWordPosition,
    byte MatchLength);

/// <summary>
/// Описывает дополнительное правило для сортировки
/// </summary>
/// <param name="Name"></param>
/// <param name="Score"></param>
/// <param name="Multipler"></param>
public record AdditionalRule(string Name, int Score = 0, double Multipler = 1);

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

    public readonly int[] NGrammsHashes = NgrammsWordsSearchHelper.GetNgramms(word);

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
