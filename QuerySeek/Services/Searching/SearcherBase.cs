using System.Runtime.CompilerServices;
using QuerySeek.Models;
using QuerySeek.Services.Helpers;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching.Requests;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Позволяет определить стратегию поиска
/// </summary>
/// <typeparam name="TContext"></typeparam>
/// <param name="nameTokenizer"></param>
/// <param name="normalizer"></param>
public abstract class SearcherBase<TContext>(INormalizer normalizer, INameTokenizer nameTokenizer) where TContext : SearchContextBase
{
    #region Overrides
    /// <summary>
    /// Определяет запрос на поиск в индексе - что ищем в индексе
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public abstract IEnumerable<RequestBase> GetRequest(TContext context);

    /// <summary>
    /// Позволяет определить сортировку
    /// </summary>
    /// <param name="context"></param>
    /// <param name="result">Отсортированный по количеству совпадений enumerable сущностей</param>
    /// <returns></returns>
    public virtual IEnumerable<EntitySearchResult> Ranging(TContext context, IEnumerable<EntitySearchResult> result)
        => result.OrderByDescending(i => i.ScoreWithRules);

    /// <summary>
    /// Позволяет осуществить предпроцессинг, указать выборку сущностей на сортировку, добавить правила
    /// </summary>
    /// <param name="context"></param>
    /// <param name="type"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public virtual IEnumerable<EntitySearchResult> TypeResultPreprocessing(TContext context, byte type, ICollection<EntitySearchResult> result)
        => result;

    /// <summary>
    /// Вызывается после вычисления всех совпадений со словами из запроса
    /// </summary>
    /// <param name="context"></param>
    /// <param name="entity"></param>
    /// <param name="summaryMatches"></param>
    public virtual void OnEntityMatched(TContext context, EntitySearchResult entity, in Span<WordMatch> summaryMatches) { }

    /// <summary>
    /// Определение настроек поиска по словам
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual WordsSearchSettings GetWordsSearchSettings(TContext context)
        => WordsSearchSettings.Default;

    /// <summary>
    /// Определение возможных альтернатив слов из запроса
    /// </summary>
    /// <returns></returns>
    public virtual Dictionary<string, string[]> GetQueryWordsAlternatives(TContext context)
        => [];

    /// <summary>
    /// Определяем моножители для слов из запроса (можем уменьшать значимость предлогов и тд)
    /// </summary>
    /// <returns></returns>
    public virtual Dictionary<string, double> GetQueryWordsMultiplers(TContext context)
        => [];

    /// <summary>
    /// Позволяет при совпадении линка, добавить просчет его звасимостей
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="linkedType"></param>
    /// <returns></returns>
    public virtual bool OnLinkedMatchNeedMergeLinks(byte entityType, byte linkedType)
        => false;

    /// <summary>
    /// Множитель совпадений из связанных сущностей
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="linkedType"></param>
    /// <returns></returns>
    public virtual double GetLinkedEntityMatchMultipler(byte entityType, byte linkedType)
        => 1;

    /// <summary>
    /// Множитель типа имени
    /// </summary>
    /// <param name="nameType"></param>
    /// <returns></returns>
    public virtual double GetNameTypeMultipler(byte nameType)
        => 1;

    #endregion

    #region Search logic
    /// <summary>
    /// Поиск топа всех типов
    /// </summary>
    /// <param name="context">Контекст поиска</param>
    /// <param name="take">Количество элементов</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Отранжированный результат из всех типов</returns>
    public EntitySearchResult[] Search(
        TContext context,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        FillContext(context);
        ProcessRequests(context, ct);

        return [.. Ranging(context, GetAllResults()).Take(take)];

        IEnumerable<EntitySearchResult> GetAllResults()
        {
            foreach (KeyValuePair<byte, Dictionary<Key, EntitySearchResult>> typeResults in context.SearchResult)
            {
                foreach (EntitySearchResult item in TypeResultPreprocessing(context, typeResults.Key, typeResults.Value.Values))
                    yield return item;
            }
        }
    }

    /// <summary>
    /// Поиск топов по типам
    /// </summary>
    /// <param name="context">Контекст поиска</param>
    /// <param name="take">Количество элементов каждого типа</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Блоки отранжированных результатов по типам</returns>
    public TypeSearchResult[] SearchTypes(
        TContext context,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        FillContext(context);
        ProcessRequests(context, ct);

        TypeSearchResult[] result = [.. context.SearchResult.Select(typeSearchResult =>
        {
            byte currentType = typeSearchResult.Key;

            IEnumerable<EntitySearchResult> preprocessed = TypeResultPreprocessing(context, currentType, typeSearchResult.Value.Values);

            EntitySearchResult[] typeResult = [.. Ranging(context, preprocessed).Take(take)];

            return new TypeSearchResult(currentType, typeResult);
        }).Where(i => i.Result.Length != 0)];

        return result;
    }

    public void FillContext(TContext context)
    {
        context.SplittedAndNormalizedQuery = TextPreprocessor.PreprocessText(nameTokenizer, normalizer, context.Query);
        context.WordsSearchSettings = GetWordsSearchSettings(context);

        Dictionary<string, string[]> alternativeWords = GetQueryWordsAlternatives(context);
        Dictionary<string, double> queryWordMultiplers = GetQueryWordsMultiplers(context);

        context.SearchWordsBundle = NgrammsWordsSearchHelper.CreateSearchWordsBundle(context, alternativeWords, queryWordMultiplers);
    }

    public void ProcessRequests(TContext context, CancellationToken ct)
    {
        foreach (RequestBase request in GetRequest(context))
        {
            request.ProcessRequest(context, ct);

            if (!context.TryGetResultsByType(request.TargetType, out Dictionary<Key, EntitySearchResult>? result))
                continue;

            foreach (EntitySearchResult item in result.Values)
                CalculateTextScore(context, item);
        }
    }

    /// <summary>
    /// Просчитываем скор текстовых совпадний для сущности
    /// </summary>
    public void CalculateTextScore(TContext context, EntitySearchResult entityMatchesBundle)
    {
        if (entityMatchesBundle.Score != 0) return;

        byte currentEntityType = entityMatchesBundle.Key.Type;

        Span<WordMatch> queryWordMatches = stackalloc WordMatch[context.SplittedAndNormalizedQuery.Length];

        //Просчитываем совпадения слов из запроса для сущнности и ее линков
        foreach ((byte Type, List<WordMatch> Matches) in GetMatches(context, entityMatchesBundle))
        {
            double linkMultipler = GetLinkedEntityMatchMultipler(currentEntityType, Type);
            if (linkMultipler != 0)
            {
                ProcessNodeMatches(in queryWordMatches, Matches, context, linkMultipler);
            }
        }

        //Складываем скор совпадений слов
        int resultScore = 0;
        foreach (WordMatch ws in queryWordMatches)
            resultScore += ws.Score;

        entityMatchesBundle.Score = resultScore;

        OnEntityMatched(context, entityMatchesBundle, in queryWordMatches);
    }

    /// <summary>
    /// Возвращает набор совпавших слов для сущности и прилинкованных к ней
    /// </summary>
    private IEnumerable<(byte Type, List<WordMatch> Matches)> GetMatches(TContext context, EntitySearchResult entityMatchesBundle)
    {
        byte currentEntityType = entityMatchesBundle.Key.Type;

        //При OnLinkedMatchNeedMergeLinks могут повторяться типы, отсекаем
        BitArray256 matchedTypes = new();

        yield return (currentEntityType, entityMatchesBundle.WordsMatches);
        matchedTypes[currentEntityType] = true;

        foreach (Key linkKey in entityMatchesBundle.Meta.Links)
        {
            if (matchedTypes[linkKey.Type]
                || !context.TryGetSearchedEntity(linkKey, out EntitySearchResult? link))
                continue;

            yield return (linkKey.Type, link.WordsMatches);
            matchedTypes[linkKey.Type] = true;

            //Пробуем провалится на уровень выше по условию и просчитать линков с данного родителя
            if (OnLinkedMatchNeedMergeLinks(currentEntityType, linkKey.Type))
            {
                Key[] linkedEntityLinks = link.Meta.Links;

                foreach (Key mergedEntityLink in linkedEntityLinks)
                {
                    if (matchedTypes[mergedEntityLink.Type]
                        || !context.TryGetSearchedEntity(mergedEntityLink, out EntitySearchResult? mergeLink))
                        continue;

                    yield return (mergedEntityLink.Type, mergeLink.WordsMatches);
                    matchedTypes[mergedEntityLink.Type] = true;
                }
            }
        }
    }

    /// <summary>
    /// Заполняет матчи для слов из запроса <paramref name="queryWordsMatches"/> из совпадений для сущности <paramref name="matches"/>
    /// </summary>
    /// <param name="queryWordsMatches">Совпадения со словами из запроса</param>
    /// <param name="matches">Матчи слов с сущностью</param>
    /// <param name="context">Контекст поиска</param>
    /// <param name="nodeMultipler">Мультиплер для линка</param>
    private void ProcessNodeMatches(in Span<WordMatch> queryWordsMatches, List<WordMatch> matches, TContext context, double nodeMultipler)
    {
        //Сначала выбираем матчи по сущности пытаемся собрать совпадения для слов из запроса
        Span<WordMatch> nodeScores = stackalloc WordMatch[queryWordsMatches.Length];
        for (int i = 0; i < matches.Count; i++)
        {
            WordMatch compareResult = matches[i];
            int queryWordPosition = GetCurrentQueryWordPosition(in nodeScores, compareResult.WordsBundlePosition);
            WordMatch previouslyCalculatedResult = nodeScores[queryWordPosition];

            bool isNewQueryPosition = false;
            if (PreviouslyMatchedWordToNewPositionName(compareResult, previouslyCalculatedResult))
            {
                isNewQueryPosition = true;
                queryWordPosition = GetNewQueryWordPosition(in nodeScores, compareResult.WordsBundlePosition);
            }

            if (queryWordPosition == -1) continue;

            double nameTypeMultipler = GetNameMultiplerInternal(compareResult.NameType);

            byte score = (byte)Math.Ceiling(compareResult.Score * nameTypeMultipler * nodeMultipler);

            if (isNewQueryPosition || previouslyCalculatedResult.Score < score)
                nodeScores[queryWordPosition] = new(compareResult.NameWordPosition, compareResult.NameType, compareResult.WordsBundlePosition, score);
        }

        //Мерж между линками
        for (int i = 0; i < queryWordsMatches.Length; i++)
        {
            WordMatch nodeMatch = nodeScores[i];
            WordMatch previouslyCalculatedResult = queryWordsMatches[i];

            if (nodeMatch.IsEmpty) continue;
            if (!previouslyCalculatedResult.IsEmpty)
            {
                int nextQueryWordPosition = GetNewQueryWordPosition(queryWordsMatches, nodeMatch.WordsBundlePosition);
                if (nextQueryWordPosition != -1) queryWordsMatches[nextQueryWordPosition] = nodeMatch;
            }
            else if (previouslyCalculatedResult.Score < nodeMatch.Score)
            {
                queryWordsMatches[i] = nodeMatch;
            }
        }

        int GetNewQueryWordPosition(in Span<WordMatch> scores, int wordsBundlePosition)
        {
            foreach (int position in context.SearchWordsBundle[wordsBundlePosition].PositionsInRequest)
            {
                if (scores[position].IsEmpty) return position;
            }

            return -1;
        }

        int GetCurrentQueryWordPosition(in Span<WordMatch> scores, int wordsBundlePosition)
        {
            int previousNotEmpty = -1;

            int[] positions = context.SearchWordsBundle[wordsBundlePosition].PositionsInRequest;

            foreach (int position in context.SearchWordsBundle[wordsBundlePosition].PositionsInRequest)
            {
                if (scores[position].IsEmpty)
                {
                    if (previousNotEmpty != -1) return previousNotEmpty;
                }
                else
                {
                    previousNotEmpty = position;
                }
            }

            return positions[0];
        }

        static bool PreviouslyMatchedWordToNewPositionName(WordMatch compareResult, WordMatch previouslyCalculatedResult)
            => !previouslyCalculatedResult.IsEmpty
                && previouslyCalculatedResult.NameType == compareResult.NameType
                && previouslyCalculatedResult.NameWordPosition != compareResult.NameWordPosition
                && previouslyCalculatedResult.WordsBundlePosition == compareResult.WordsBundlePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double GetNameMultiplerInternal(byte nameType)
    {
        if (nameType == 0)
            return 1;

        return GetNameTypeMultipler(nameType);
    }
    #endregion

}
