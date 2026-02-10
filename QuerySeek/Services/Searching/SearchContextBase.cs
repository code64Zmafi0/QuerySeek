using System.Runtime.InteropServices;
using QuerySeek.Models;
using QuerySeek.Services.Searching.Requests;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Контекст поиска. Абстрактный класс. Определяем запрос, можем хранить дополнительные свойства
/// </summary>
/// <param name="index"></param>
/// <param name="query"></param>
public abstract class SearchContextBase(IndexInstance index, string query)
{
    #region Overrides
    public abstract RequestBase[] GetRequest();

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

    public Dictionary<byte, Dictionary<Key, EntityMatchesBundle>> SearchResult { get; set; } = [];

    #region Search Tools
    public Dictionary<Key, EntityMatchesBundle>? GetResultsByType(byte type)
    {
        if (SearchResult.TryGetValue(type, out var result))
            return result;

        return null;
    }

    public void AddResult(Key key, EntityMeta meta)
    {
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, meta);
    }

    public void AddResult(Key key, EntityMeta entityMeta, byte nameWordPosition, byte phraseType, byte queryWordPosition, byte matchLength)
    {
        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(SearchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, entityMeta);

        matchesBundle!.AddMatch(new(nameWordPosition, phraseType, queryWordPosition, matchLength));
    }
    #endregion
}
