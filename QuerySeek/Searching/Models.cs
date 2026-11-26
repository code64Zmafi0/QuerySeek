using System.Data;
using QuerySeek.Models;

namespace QuerySeek.Searching;

/// <summary>
/// Описывает найденную сущность
/// </summary>
/// <remarks>
/// <see cref="Key">Ключ сущности</see><br/>
/// <see cref="Meta">Информация о линках и потомках</see><br/>
/// <see cref="Matches">Совпавшие токены данной сущности</see><br/>
/// <see cref="Score">Суммарный скор</see><br/>
/// <see cref="SummaryMatchesCount">Суммарное количество совпавших токенов</see><br/>
/// </remarks>
public class EntitySearchResult(Key key, EntityMeta meta)
{
    public readonly Key Key = key;

    public readonly EntityMeta Meta = meta;

    public readonly List<TokenMatch> Matches = new(1);

    public int Score { get; set; }

    public int SummaryMatchesCount { get; set; }

    public IEnumerable<Key> GetLinks(byte type)
        => Meta.Links.Where(i => i.Type == type);

    public IEnumerable<Key> GetChilds(byte type)
        => Meta.Childs.Where(i => i.Type == type);
}

/// <summary>
/// Описывает совпавщий токен c именем сущности
/// </summary>
/// <remarks>
/// <see cref="NameTokenPosition">Позиция токена в имени сущности</see><br/>
/// <see cref="NameType">Тип имени сущности</see><br/>
/// <see cref="SearchTokenIndex">Позиция токена в SearchTokens</see><br/>
/// <see cref="Score">Дистанция совпадения - скоринг за совпавшие ngamm-ы</see><br/>
/// </remarks>
/// <param name="NameTokenPosition">Позиция совпавшего токена в имени</param>
/// <param name="NameType">Тип имени</param>
/// <param name="SearchTokenIndex">Позиция токена в SearchTokens</param>
/// <param name="Score">Длина совпадения (по количеству совпавщих нграмм)</param>
public readonly record struct TokenMatch(
    byte NameTokenPosition,
    byte NameType,
    byte SearchTokenIndex,
    byte Score)
{
    public bool IsEmpty => Score == 0;
}

/// <summary>
/// Контейнер слова из запроса включает в себя слово из запроса, альтернативы, позиции в запросе и похожие слова из индекса
/// </summary>
/// <param name="QueryWordAndAlternatives">Слово из запроса на 0 позиции и альтернативы</param>
/// <param name="PositionsInRequest">Позиции данного слова в исходном запросе</param>
/// <param name="SimilarWords">Набор схожих слов из индекса</param>
public record QueryTokenContainer(List<Word> QueryWordAndAlternatives, int[] PositionsInRequest, List<KeyValuePair<int, byte>> SimilarWords)
{
    /// <summary>
    /// Введенное слово
    /// </summary>
    public Word Word => QueryWordAndAlternatives[0];
}

/// <summary>
/// Слово из запроса интерпретированное в нграммы
/// </summary>
public class Word(string word, int[] ngramms, double multiplier) : IEquatable<Word>
{
    public readonly string WordText = word;

    public readonly int[] NGrammsHashes = ngramms;

    public readonly double Multiplier = multiplier;

    public readonly bool IsDigit = long.TryParse(word, out _);

    public bool Equals(Word? other)
    {
        if (other is null) return false;

        return other.WordText.Equals(WordText);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Word w)
            return false;

        return Equals(w);
    }

    public override int GetHashCode()
        => WordText.GetHashCode();
}
