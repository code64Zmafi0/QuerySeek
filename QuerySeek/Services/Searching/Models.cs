using QuerySeek.Models;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Описывает найденную сущность
/// </summary>
/// <remarks>
/// <see cref="Key">Ключ сущности</see><br/>
/// <see cref="Meta">Информация о линках и потомках</see><br/>
/// <see cref="Matches">Совпавшие слова данной сущности с запросом</see><br/>
/// <see cref="Rules">Дополнительные правила</see><br/>
/// <see cref="Score">Скор набранный калькулятором</see><br/>
/// <see cref="ScoreWithRules">Скор набранный калькулятором c учетом правил</see><br/>
/// </remarks>
public class EntitySearchResult(Key key, EntityMeta meta)
{
    public readonly Key Key = key;

    public readonly EntityMeta Meta = meta;

    public readonly List<TokenMatch> Matches = new(1);

    public readonly List<AdditionalRule> Rules = [];

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

    /// <summary>
    /// Скор набранный калькулятором
    /// </summary>
    public int Score;

    /// <summary>
    /// Скор набранный калькулятором с учетом правил
    /// </summary>
    public int ScoreWithRules
    {
        get
        {
            int resultScore = Score;

            for (int i = 0; i < Rules.Count; i++)
            {
                AdditionalRule item = Rules[i];

                resultScore += item.Score;
                resultScore = (int)(resultScore * item.Multiplier);
            }

            return resultScore;
        }
    }
}

/// <summary>
/// Описывает совпавщее слово c именем сущности
/// </summary>
/// <remarks>
/// <see cref="NameTokenPosition">Позиция слова в имени сущности</see><br/>
/// <see cref="NameType">Тип имени сущности</see><br/>
/// <see cref="SearchTokensBundlePosition">Позиция слова в запросе</see><br/>
/// <see cref="Score">Дистанция совпадения - скоринг за совпавшие ngamm-ы</see><br/>
/// </remarks>
/// <param name="NameTokenPosition">Позиция совпавшего слова в имени</param>
/// <param name="NameType">Тип имени</param>
/// <param name="SearchTokensBundlePosition">Позиция совпавшего слова из запроса</param>
/// <param name="Score">Длина совпадения (по количеству совпавщих нграмм)</param>
public readonly record struct TokenMatch(
    byte NameTokenPosition,
    byte NameType,
    byte SearchTokensBundlePosition,
    byte Score)
{
    public bool IsEmpty => Score == 0;
}

/// <summary>
/// Описывает дополнительное правило для сортировки
/// </summary>
/// <param name="Name">Имя правила</param>
/// <param name="Score">Начисляемый скор</param>
/// <param name="Multiplier">Множитель</param>
/// <param name="Property">Кастомное свойство</param>
public record AdditionalRule(string Name, int Score = 0, double Multiplier = 1, object? Property = null);

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
