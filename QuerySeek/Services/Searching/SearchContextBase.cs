using System.Runtime.InteropServices;
using QuerySeek.Models;
using QuerySeek.Services.Searching.Requests;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Контекст поиска, можем хранить дополнительные свойства при переопределении
/// </summary>
/// <param name="index"></param>
/// <param name="query"></param>
public class SearchContextBase(IndexInstance index, string query)
{
    #region Overrides
    public virtual HashSet<string> NotRealivatedWords { get; } = [];

    /// <summary>
    /// Альтернативные слова вида (III -> 3)
    /// </summary>
    public virtual Dictionary<string, string[]> AlternativeWords { get; } = [];
    #endregion

    public RequestBase[] Request { get; internal set; } = [];

    public IndexInstance Index { get; set; } = index;

    public string Query { get; set; } = query;

    public string[] SplittedAndNormalizedQuery { get; set; } = [];

    public QueryWordContainer[] NgrammedQuery { get; set; } = [];

    public Dictionary<byte, Dictionary<Key, EntitySearchResult>> SearchResult { get; set; } = [];

    #region Search Tools
    public int FullQueryScore => NgrammedQuery.Sum(i => i.QueryWord.NGrammsHashes.Length);

    public Dictionary<Key, EntitySearchResult>? GetResultsByType(byte type)
    {
        if (SearchResult.TryGetValue(type, out var result))
            return result;

        return null;
    }

    public void AddResult(Key key)
    {
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, Index.Entities[key]);
    }

    public void AddResult(Key key, WordCompareResult wordCompareResult)
    {
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref EntitySearchResult? matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, Index.Entities[key]);

        matchesBundle!.AddMatch(wordCompareResult);
    }
    #endregion
}
