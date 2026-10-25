using System.Runtime.InteropServices;
using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Базовое определние для запроса на выборку в индексе. Шаг SELECTING
/// </summary>
/// <param name="targetType">Текущий тип отбираемых сущностей</param>
public abstract class EntitiesSelectionOperation(byte targetType)
{
    public byte TargetType { get; } = targetType;

    /// <summary>
    /// Выполняет процесс поиска сущностей в индексе и заполняет результат в SearchContext
    /// </summary>
    /// <param name="searchContext">Контекст поиска</param>
    /// <param name="ct">Токен отмены</param>
    public abstract void Process(SearchContextBase searchContext, CancellationToken ct);

    /// <summary>
    /// Добавляет в контекст поиска сущность
    /// </summary>
    public static void AddResult(SearchContextBase searchContext, Key key)
    {
        Dictionary<byte, Dictionary<Key, EntitySearchResult>> searchResult = searchContext.SearchResult;
        IReadOnlyDictionary<Key, EntityMeta> entities = searchContext.Index.Entities;

        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(searchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, entities[key]);
    }

    /// <summary>
    /// Добавляет в контекст поиска сущность и добавляет совпадение со словом из запроса
    /// </summary>
    public static void AddResult(SearchContextBase searchContext, Key key, WordMatch wordMatch)
    {
        Dictionary<byte, Dictionary<Key, EntitySearchResult>> searchResult = searchContext.SearchResult;
        IReadOnlyDictionary<Key, EntityMeta> entities = searchContext.Index.Entities;

        ref var types = ref CollectionsMarshal.GetValueRefOrAddDefault(searchResult, key.Type, out var exists);

        if (!exists)
            types = [];

        ref EntitySearchResult? matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(types!, key, out exists);

        if (!exists)
            matchesBundle = new(key, entities[key]);

        matchesBundle!.WordsMatches.Add(wordMatch);
    }
}
