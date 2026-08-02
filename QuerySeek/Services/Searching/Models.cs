using QuerySeek.Models;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Searching;

public class TypeSearchResult(byte type, EntitySearchResult[] result)
{
    public readonly byte Type = type;

    public readonly EntitySearchResult[] Result = result;
}

/// <summary>
/// Описывает найденную сущность
/// </summary>
/// <remarks>
/// Содержит:
/// Key - ключ сущности; 
/// Meta - информацию о линках и потомков;
/// WordsMatches - совпавшие слова из имен сущности;
/// Rules - Дополнительные правила;
/// Prescore - Суммарная дистанция сопадениий из имен;
/// Score - Конечный скор для сортировки;
/// </remarks>
/// <param name="key"></param>
/// <param name="meta"></param>
public class EntitySearchResult(Key key, EntityMeta meta)
{
    public readonly Key Key = key;

    public readonly EntityMeta Meta = meta;

    public readonly List<WordCompareResult> WordsMatches = new(1);

    public readonly List<AdditionalRule> Rules = [];

    public int Score;

    public bool ContainsQueryWord(int queryWordIndex)
    {
        for (int i = 0; i < WordsMatches.Count; i++)
        {
            if (WordsMatches[i].QueryWordPosition == queryWordIndex)
                return true;
        }

        return false;
    }

    public Key? TryGetLink(byte type)
    {
        foreach (Key link in Meta.Links)
            if (link.Type == type) return link;

        return null;
    }

    public Key? TryGetChild(byte type)
    {
        foreach (Key link in Meta.Childs)
            if (link.Type == type) return link;

        return null;
    }

    public void AddRule(AdditionalRule rule)
        => Rules.Add(rule);
}

/// <summary>
/// Описывает совпавщее слово c именем сущности
/// </summary>
/// <remarks>
/// Содержит: Позицию слова в имени; Тип имени; Позицию слова в запросе; Дистанцию совпадения - скоринг за совпавшие ngamm-ы.
/// </remarks>
/// <param name="NameWordPosition">Позиция совпавшего слова в имени</param>
/// <param name="NameType">Тип имени</param>
/// <param name="QueryWordPosition">Позиция совпавшего слова из запроса</param>
/// <param name="MatchLength">Длина совпадения (по количеству свопавщих нграмм)</param>
public readonly record struct WordCompareResult(
    byte NameWordPosition,
    byte NameType,
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
