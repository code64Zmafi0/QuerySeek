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

    public readonly List<WordMatch> WordsMatches = new(1);

    public readonly List<AdditionalRule> Rules = [];

    public int Score;

    public Key? GetLink(byte type)
    {
        foreach (Key link in Meta.Links)
            if (link.Type == type) return link;

        return null;
    }

    public IEnumerable<Key> GetChilds(byte type)
        => Meta.Childs.Where(i => i.Type == type);

    public void AddRule(AdditionalRule rule)
        => Rules.Add(rule);

    public int ScoreWithRules
    {
        get
        {
            int resultScore = Score;

            for (int i = 0; i < Rules.Count; i++)
            {
                AdditionalRule item = Rules[i];

                resultScore += item.Score;
                resultScore = (int)(resultScore * item.Multipler);
            }

            return resultScore;
        }
    }
}

/// <summary>
/// Описывает совпавщее слово c именем сущности
/// </summary>
/// <remarks>
/// Содержит: Позицию слова в имени; Тип имени; Позицию слова в запросе; Дистанцию совпадения - скоринг за совпавшие ngamm-ы.
/// </remarks>
/// <param name="NameWordPosition">Позиция совпавшего слова в имени</param>
/// <param name="NameType">Тип имени</param>
/// <param name="WordsBundlePosition">Позиция совпавшего слова из запроса</param>
/// <param name="Score">Длина совпадения (по количеству свопавщих нграмм)</param>
public readonly record struct WordMatch(
    byte NameWordPosition,
    byte NameType,
    byte WordsBundlePosition,
    byte Score)
{
    public bool IsEmpty => Score == 0;
}

/// <summary>
/// Описывает дополнительное правило для сортировки
/// </summary>
/// <param name="Name"></param>
/// <param name="Score"></param>
/// <param name="Multipler"></param>
public record AdditionalRule(string Name, int Score = 0, double Multipler = 1);

/// <summary>
/// Контейнер слова из запроса включает в себя слово из запроса, альтернативы, позиции в запросе и похожие слова из индекса
/// </summary>
/// <param name="QueryWordAndAlternatives">Слово из запроса на 0 позиции и альтернативы</param>
/// <param name="PositionsInRequest">Позиции данного слова в исходном запросе</param>
/// <param name="SimilarWords">Набор схожих слов из индекса</param>
public record QueryWordContainer(List<Word> QueryWordAndAlternatives, int[] PositionsInRequest, List<KeyValuePair<int, byte>> SimilarWords);

/// <summary>
/// Слово из запроса интерпретированное в нграммы
/// </summary>
/// <param name="word"></param>
/// <param name="ngramms"></param>
/// <param name="multipler"></param>
public class Word(string word, int[] ngramms, double multipler) : IEquatable<Word>
{
    public readonly string QueryWord = word;

    public readonly int[] NGrammsHashes = ngramms;

    public readonly double Multiplier = multipler;

    public readonly bool IsDigit = long.TryParse(word, out _);

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
