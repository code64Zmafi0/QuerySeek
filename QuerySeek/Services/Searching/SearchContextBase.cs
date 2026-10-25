using System.Diagnostics.CodeAnalysis;
using QuerySeek.Models;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching.WordsSearch;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Контекст поиска, можем хранить дополнительные свойства при переопределении
/// </summary>
/// <param name="index">Индекс</param>
/// <param name="query">Поисковая строка</param>
public class SearchContextBase(
    IndexInstance index,
    string query)
{
    /// <summary>
    /// Входящий текстовый запрос
    /// </summary>
    public string Query { get; } = query;

    /// <summary>
    /// Инстанс индекса
    /// </summary>
    public IndexInstance Index { get; } = index;

    /// <summary>
    /// Используемый токенайзер
    /// </summary>
    public ITokenizer Tokenizer { get; internal set; } = DefaultTokenizer.Instance;

    /// <summary>
    /// Используемый нормалайзер
    /// </summary>
    public INormalizer Normalizer { get; internal set; } = DefaultNormalizer.Instance;

    /// <summary>
    /// Нормализованный и разбитый по словам запрос
    /// </summary>
    public string[] SplittedAndNormalizedQuery { get; internal set; } = [];

    /// <summary>
    /// Бандл слов для поиска
    /// </summary>
    public QueryWordContainer[] SearchWordsBundle { get; internal set; } = [];

    /// <summary>
    /// Настройки поиска слов
    /// </summary>
    public WordsSearchSettings WordsSearchSettings { get; internal set; } = WordsSearchSettings.Default;

    /// <summary>
    /// Бандл результатов поиска сущностей в индексе
    /// </summary>
    public Dictionary<byte, Dictionary<Key, EntitySearchResult>> SearchResult { get; } = [];

    /// <summary>
    /// Пробует получить найденную сущность в контексте
    /// </summary>
    public bool TryGetSearchedEntity(Key key, [NotNullWhen(true)] out EntitySearchResult? searchResult)
    {
        searchResult = null;
        return SearchResult.TryGetValue(key.Type, out var entities) && entities.TryGetValue(key, out searchResult);
    }

    /// <summary>
    /// Пробует получить словарь результатов для определенного типа
    /// </summary>
    public bool TryGetResultsByType(byte type, [NotNullWhen(true)] out Dictionary<Key, EntitySearchResult>? result)
        => SearchResult.TryGetValue(type, out result);

    /// <summary>
    /// Отдает коллекцию результатов запрошенного типа
    /// </summary>
    public ICollection<EntitySearchResult> GetResults(byte type)
        => TryGetResultsByType(type, out Dictionary<Key, EntitySearchResult>? result)
            ? result.Values
            : Array.Empty<EntitySearchResult>();
}
