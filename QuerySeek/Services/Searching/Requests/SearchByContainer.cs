using System.Runtime.CompilerServices;
using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Выполняет поиск сущностей целевого типа по найденным контейнерам
/// </summary>
/// <param name="targetType">Целевой тип сущности</param>
/// <param name="containerType">Тип сущности родителя (Parent)</param>
/// <param name="filter">Фильтр добавления в словарь найденных</param>
/// <param name="containersFilter">Фильтр родителей по которым осуществляем поиск</param>
public class SearchByContainer(
    byte targetType,
    byte containerType,
    Func<Key, bool>? filter = null,
    Func<IEnumerable<EntitySearchResult>, IEnumerable<EntitySearchResult>>? containersFilter = null) : RequestBase(targetType)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual Key[] SelectParents(Dictionary<Key, EntitySearchResult> byStrat)
    {
        Key[] result = [];

        if (containersFilter is null)
        {
            result = new Key[byStrat.Count];
            byStrat.Keys.CopyTo(result, 0);
        }
        else
        {
            result = [.. containersFilter.Invoke(byStrat.Values).Select(i => i.Key)];
        }

        return result;
    }

    public override void ProcessRequest(
        SearchContextBase searchContext,
        List<KeyValuePair<int, byte>>[] wordsBundle,
        WordsSearchSettings wordsSearchSettings,
        CancellationToken ct)
    {
        EntitiesByWordsIndex entitiesByWordsIndex = searchContext.Index.EntitiesByWordsIndex;

        if (!(searchContext.GetResultsByType(containerType) is { } byStrat))
            return;

        Key[] containers = SelectParents(byStrat);

        for (byte queryWordPosition = 0; queryWordPosition < wordsBundle.Length; queryWordPosition++)
        {
            List<KeyValuePair<int, byte>> currentBundle = wordsBundle[queryWordPosition];

            WordsSearchManager wsm = wordsSearchSettings.GetWordsSearchManager();

            for (int i = 0; i < currentBundle.Count; i++)
            {
                if (!wsm.NeedContinue)
                    break;

                KeyValuePair<int, byte> indexWordInfo = currentBundle[i];

                int wordId = indexWordInfo.Key;

                bool isMatchedWord = false;
                foreach (var wordMatchMeta in entitiesByWordsIndex.GetMatchesByWordAndParents(
                    wordId,
                    TargetType,
                    containers))
                {
                    if (ct.IsCancellationRequested)
                        return;

                    isMatchedWord = true;

                    Key entityKey = new(TargetType, wordMatchMeta.EntityId);

                    if (!((filter?.Invoke(entityKey)) ?? true))
                        continue;

                    WordCompareResult wcr = new(
                        wordMatchMeta.NameWordPosition,
                        wordMatchMeta.PhraseType,
                        queryWordPosition,
                        indexWordInfo.Value);

                    searchContext.AddResult(entityKey, wcr);
                }

                if (isMatchedWord) wsm.IncrementMatch();
            }
        }
    }
}
