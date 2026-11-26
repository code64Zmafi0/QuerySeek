using System.Diagnostics.CodeAnalysis;
using QuerySeek.Models;

namespace QuerySeek.Searching;

/// <summary>
/// Базовый контекст поиска QuerySeek
/// </summary>
/// <remarks>
/// <see cref="Query">Переданный текстовый запрос</see><br/>
/// <see cref="Index">Переданный инстанс индекса</see><br/>
/// <see cref="SplittedAndNormalizedQuery">Нормализованный и разбитый по словам текстовый запрос</see><br/>
/// <see cref="SearchTokens">Бандл токенов запроса для осуществления поиска по индексу</see><br/>
/// <see cref="SearchResult">Коллекция результатов поиска по индексу</see>
/// <para>Вспомогательные методы:</para>
/// <see cref="TryGetSearchedEntity"/><br/>
/// <see cref="TryGetResultsByType"/><br/>
/// <see cref="GetResults"/><br/>
/// <see cref="GetAllResults"/><br/>
/// </remarks>
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
    /// Нормализованный и разбитый по словам запрос
    /// </summary>
    public string[] SplittedAndNormalizedQuery { get; internal set; } = [];

    /// <summary>
    /// Коллекция слов для поиска
    /// </summary>
    public QueryTokenContainer[] SearchTokens { get; internal set; } = [];

    /// <summary>
    /// Бандл результатов поиска сущностей в индексе
    /// </summary>
    public Dictionary<byte, Dictionary<Key, EntitySearchResult>> SearchResult { get; } = [];

    /// <summary>
    /// Пробует получить найденную сущность
    /// </summary>
    public bool TryGetSearchedEntity(Key key, [NotNullWhen(true)] out EntitySearchResult? searchResult)
    {
        searchResult = null;
        return SearchResult.TryGetValue(key.Type, out var entities) && entities.TryGetValue(key, out searchResult);
    }

    /// <summary>
    /// Пробует получить словарь результатов для определенного типа
    /// </summary>
    public bool TryGetResultsByType(byte type, [NotNullWhen(true)] out Dictionary<Key, EntitySearchResult>? result) =>
        SearchResult.TryGetValue(type, out result);

    /// <summary>
    /// Возвращает коллекцию результатов запрошенного типа
    /// </summary>
    public ICollection<EntitySearchResult> GetResults(byte type) =>
        TryGetResultsByType(type, out Dictionary<Key, EntitySearchResult>? result)
            ? result.Values
            : Array.Empty<EntitySearchResult>();

    /// <summary>
    /// Возвращает перечисление всех результатов поиска
    /// </summary>
    public IEnumerable<EntitySearchResult> GetAllResults() =>
        SearchResult.Values.SelectMany(i => i.Values);
}
