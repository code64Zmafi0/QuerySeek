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
    /// Позволяет переопределить конечную сортировку
    /// </summary>
    /// <param name="context"></param>
    /// <param name="result">Отсортированный по количеству совпадений enumerable сущностей</param>
    /// <returns></returns>
    public virtual IOrderedEnumerable<EntitySearchResult> PostProcessing(TContext context, IOrderedEnumerable<EntitySearchResult> result)
        => result;

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

    /// <summary>
    /// Вызывается после вычисления совпадений со словами из запроса
    /// </summary>
    /// <param name="context"></param>
    /// <param name="entity"></param>
    /// <param name="summaryMatches"></param>
    public virtual void OnEntityMatched(TContext context, EntitySearchResult entity, in Span<WordCompareResult> summaryMatches) { }

    /// <summary>
    /// Определение настроек поиска по словам
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual WordsSearchSettings GetWordsSearchSettings(TContext context)
        => context.SearchWordsBundle.Length > 5
            ? WordsSearchSettings.Fast
            : WordsSearchSettings.Default;

    /// <summary>
    /// Определяем возможные альтернативные слова для слов из запроса
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

    #endregion

    #region Search logic
    /// <summary>
    /// Поиск топа всех типов
    /// </summary>
    /// <param name="context">Контекст поиска</param>
    /// <param name="take">Количество элементов</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public EntitySearchResult[] Search(
        TContext context,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        FillContext(context);
        ProcessRequests(context, ct);

        return [.. PostProcessing(context, GetAllResults().OrderByDescending(UseRules)).Take(take) ];

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
    /// <returns></returns>
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

            IOrderedEnumerable<EntitySearchResult> preprocessed = TypeResultPreprocessing(context, currentType, typeSearchResult.Value.Values)
                .OrderByDescending(UseRules);

            EntitySearchResult[] typeResult = [.. PostProcessing(context, preprocessed).Take(take)];

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
        context.Request = GetRequest(context);
    }

    public void ProcessRequests(TContext context, CancellationToken ct)
    {
        foreach (RequestBase request in context.Request)
        {
            request.ProcessRequest(context, ct);

            if (!context.TryGetResultsByType(request.TargetType, out Dictionary<Key, EntitySearchResult>? result))
                continue;

            foreach (EntitySearchResult item in result.Values)
                CalculateTextScore(context, item);
        }
    }

    /// <summary>
    /// Просчитываем скор текстовых сопадний для сущности
    /// </summary>
    public void CalculateTextScore(TContext context, EntitySearchResult entityMatchesBundle)
    {
        if (entityMatchesBundle.Score != 0) return;

        byte currentEntityType = entityMatchesBundle.Key.Type;
        Key[] entityLinks = entityMatchesBundle.Meta.Links;

        Span<WordCompareResult> summaryMatches = stackalloc WordCompareResult[context.SplittedAndNormalizedQuery.Length];

        //Просчитываем совпадения для слов из запроса по совпадениям из сущности и слинкованных сущностей
        foreach ((byte Type, List<WordCompareResult> Matches) in GetMatches(context, entityMatchesBundle))
        {
            double linkMultipler = GetLinkedEntityMatchMultipler(currentEntityType, Type);
            if (linkMultipler != 0)
            {
                ProcessNodeScoring(in summaryMatches, Matches, context, linkMultipler);
            }
        }

        int resultScore = 0;

        //Складываем скор для совпадений по словам из запроса
        foreach (WordCompareResult ws in summaryMatches)
            resultScore += ws.Score;

        entityMatchesBundle.Score = resultScore;

        OnEntityMatched(context, entityMatchesBundle, in summaryMatches);
    }

    /// <summary>
    /// Возвращает матчи сущности и слинкованных сущностей
    /// </summary>
    private IEnumerable<(byte Type, List<WordCompareResult> Matches)> GetMatches(TContext context, EntitySearchResult entityMatchesBundle)
    {
        byte currentEntityType = entityMatchesBundle.Key.Type;

        //При OnLinkedMatchNeedMergeLinks могут повторяться типы, отсекаем
        BitArray256 matchedTypes = new();

        yield return (currentEntityType, entityMatchesBundle.WordsMatches);
        matchedTypes[currentEntityType] = true;

        foreach (Key linkKey in entityMatchesBundle.Meta.Links)
        {
            if (matchedTypes[linkKey.Type]
                || !context.TryGetSearchedEntity(linkKey, out EntitySearchResult? linkMath))
                continue;

            yield return (linkKey.Type, linkMath.WordsMatches);
            matchedTypes[linkKey.Type] = true;

            //Пробуем провалится на уровень выше по условию и просчитать линков с данного родителя
            if (OnLinkedMatchNeedMergeLinks(currentEntityType, linkKey.Type))
            {
                Key[] linkedEntityLinks = linkMath.Meta.Links;

                foreach (Key chainedEntityLink in linkedEntityLinks)
                {
                    if (matchedTypes[chainedEntityLink.Type]
                        || !context.TryGetSearchedEntity(chainedEntityLink, out EntitySearchResult? parentLinkMathes))
                        continue;

                    yield return (chainedEntityLink.Type, parentLinkMathes.WordsMatches);
                    matchedTypes[chainedEntityLink.Type] = true;
                }
            }
        }
    }

    private void ProcessNodeScoring(in Span<WordCompareResult> wordsScores, List<WordCompareResult> matches, TContext context, double nodeMultipler)
    {
        //Сначала выбираем матчи по сущности пытаемся собрать совпадения для слов из запроса
        Span<WordCompareResult> nodeScores = stackalloc WordCompareResult[wordsScores.Length];
        for (int i = 0; i < matches.Count; i++)
        {
            WordCompareResult compareResult = matches[i];
            int queryWordPosition = GetCurrentQueryWordPosition(nodeScores, compareResult.WordsBundlePosition);
            WordCompareResult previouslyCalculatedResult = nodeScores[queryWordPosition];

            bool isNewQueryPosition = false;
            if (!previouslyCalculatedResult.IsEmpty
                && previouslyCalculatedResult.NameType == compareResult.NameType
                && previouslyCalculatedResult.NameWordPosition != compareResult.NameWordPosition
                && previouslyCalculatedResult.WordsBundlePosition == compareResult.WordsBundlePosition)
            {
                isNewQueryPosition = true;
                queryWordPosition = GetNewQueryWordPosition(nodeScores, compareResult.WordsBundlePosition);
            }

            if (queryWordPosition == -1) continue;

            double nameTypeMultipler = GetNameMultiplerInternal(compareResult.NameType);

            byte score = (byte)(compareResult.Score * nameTypeMultipler * nodeMultipler);

            if (isNewQueryPosition || previouslyCalculatedResult.Score < score)
                nodeScores[queryWordPosition] = new(compareResult.NameWordPosition, compareResult.NameType, compareResult.WordsBundlePosition, score);
        }

        //Мерж между линками
        for (int i = 0; i < wordsScores.Length; i++)
        {
            WordCompareResult nodeMatch = nodeScores[i];
            WordCompareResult previouslyCalculatedResult = wordsScores[i];

            if (nodeMatch.IsEmpty) continue;
            if (!previouslyCalculatedResult.IsEmpty)
            {
                int nextQueryWordPosition = GetNewQueryWordPosition(wordsScores, nodeMatch.WordsBundlePosition);
                if (nextQueryWordPosition != -1) wordsScores[nextQueryWordPosition] = nodeMatch;
            }
            else if (previouslyCalculatedResult.Score < nodeMatch.Score)
            {
                wordsScores[i] = nodeMatch;
            }
        }

        int GetNewQueryWordPosition(in Span<WordCompareResult> scores, int wordsBundlePosition)
        {
            foreach (int position in context.SearchWordsBundle[wordsBundlePosition].PositionsInRequest)
            {
                if (scores[position].IsEmpty) return position;
            }

            return -1;
        }

        int GetCurrentQueryWordPosition(in Span<WordCompareResult> scores, int wordsBundlePosition)
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
    }

    private static int UseRules(EntitySearchResult entitySearchResult)
    {
        int resultScore = entitySearchResult.Score;

        List<AdditionalRule> rules = entitySearchResult.Rules;
        for (int i = 0; i < rules.Count; i++)
        {
            AdditionalRule item = rules[i];

            resultScore += item.Score;
            resultScore = (int)(resultScore * item.Multipler);
        }

        entitySearchResult.Score = resultScore;
        return resultScore;
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
