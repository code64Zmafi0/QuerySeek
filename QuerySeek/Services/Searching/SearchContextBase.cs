using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using QuerySeek.Models;
using QuerySeek.Services.Searching.Requests;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Контекст поиска, можем хранить дополнительные свойства при переопределении
/// </summary>
/// <param name="index"></param>
public class SearchContextBase(IndexInstance index)
{
    /// <summary>
    /// Входящий текстовый запрос
    /// </summary>
    public string Query { get; internal set; } = string.Empty;

    /// <summary>
    /// Инстанс индекса
    /// </summary>
    public IndexInstance Index { get; internal set; } = index;

    /// <summary>
    /// Запрос на поиск в индексе
    /// </summary>
    public IEnumerable<RequestBase> Request { get; internal set; } = [];

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
    public Dictionary<byte, Dictionary<Key, EntitySearchResult>> SearchResult { get; set; } = [];

    #region Search Tools
    public int FullQueryScore => SearchWordsBundle.Sum(i => i.QueryWord.NGrammsHashes.Length);

    /// <summary>
    /// 
    /// </summary>
    public bool TryGetSearchedEntity(Key key, [NotNullWhen(true)] out EntitySearchResult? searchResult)
    {
        searchResult = null;
        return SearchResult.TryGetValue(key.Type, out var entities) && entities.TryGetValue(key, out searchResult);
    }

    public bool TryGetResultsByType(byte type, [NotNullWhen(true)] out Dictionary<Key, EntitySearchResult>? result)
        => SearchResult.TryGetValue(type, out result);

    /// <summary>
    /// Добавляет в контекст поиска сущность
    /// </summary>
    /// <param name="key"></param>
    public void AddResult(Key key)
    {
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, Index.Entities[key]);
    }

    /// <summary>
    /// Добавляет в контекст поиска сущность и добавляет свопадение со словом из запроса
    /// </summary>
    /// <param name="key"></param>
    /// <param name="wordCompareResult"></param>
    public void AddResult(Key key, WordCompareResult wordCompareResult)
    {
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref EntitySearchResult? matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, Index.Entities[key]);

        matchesBundle!.WordsMatches.Add(wordCompareResult);
    }
    #endregion
}
