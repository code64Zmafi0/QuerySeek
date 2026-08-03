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
public abstract class SearcherBase<TContext>(INameTokenizer nameTokenizer, INormalizer normalizer) where TContext : SearchContextBase
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
    public virtual IEnumerable<EntitySearchResult> TypeBundlePreprocessing(TContext context, byte type, IEnumerable<EntitySearchResult> result)
        => result;

    /// <summary>
    /// Позволяет при совпадении линка, добавить просчет его линков
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
    public virtual Dictionary<string, string[]> GetWordsAlternativesPairs(TContext context)
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
    /// <param name="query">Текстовый запрос</param>
    /// <param name="take">Количество элементов</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public EntitySearchResult[] Search(
        TContext context,
        string query,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        FillContext(context, query);
        ProcessRequests(context, ct);

        return [.. PostProcessing(context, GetAllResults().OrderByDescending(UseRules)).Take(take) ];

        IEnumerable<EntitySearchResult> GetAllResults()
        {
            foreach (KeyValuePair<byte, Dictionary<Key, EntitySearchResult>> typeResults in context.SearchResult)
            {
                foreach (EntitySearchResult item in TypeBundlePreprocessing(context, typeResults.Key, typeResults.Value.Values))
                    yield return item;
            }
        }
    }

    /// <summary>
    /// Поиск топов по типам
    /// </summary>
    /// <param name="context">Контекст поиска</param>
    /// <param name="query">Текстовый запрос</param>
    /// <param name="take">Количество элементов каждого типа</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public TypeSearchResult[] SearchTypes(
        TContext context,
        string query,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        FillContext(context, query);
        ProcessRequests(context, ct);

        TypeSearchResult[] result = [.. context.SearchResult.Select(typeSearchResult =>
        {
            byte currentType = typeSearchResult.Key;

            IOrderedEnumerable<EntitySearchResult> preprocessed = TypeBundlePreprocessing(context, currentType, typeSearchResult.Value.Values)
                .OrderByDescending(UseRules);

            EntitySearchResult[] typeResult = [.. PostProcessing(context, preprocessed).Take(take)];

            return new TypeSearchResult(currentType, typeResult);
        }).Where(i => i.Result.Length != 0)];

        return result;
    }

    public void FillContext(TContext context, string query)
    {
        context.Query = query;
        context.SplittedAndNormalizedQuery = TextPreprocessor.PreprocessName(nameTokenizer, normalizer, query);
        context.WordsSearchSettings = GetWordsSearchSettings(context);

        Dictionary<string, string[]> alternativeWords = GetWordsAlternativesPairs(context);
        Dictionary<string, double> queryWordMultiplers = GetQueryWordsMultiplers(context);

        context.SearchWordsBundle = NgrammsWordsSearchHelper.CreateSearchWordsBundle(context, alternativeWords, queryWordMultiplers);
        context.Request = GetRequest(context);
    }

    public void ProcessRequests(TContext context, CancellationToken ct)
    {
        foreach (RequestBase request in context.Request)
        {
            request.ProcessRequest(context, ct);

            if (!context.GetResultsByType(request.TargetType, out Dictionary<Key, EntitySearchResult>? result))
                continue;

            foreach (EntitySearchResult item in result.Values)
                CalculateTextScore(context, item);
        }
    }

    public void CalculateTextScore(TContext context, EntitySearchResult entityMatchesBundle)
    {
        if (entityMatchesBundle.Score != 0) return;

        byte currentEntityType = entityMatchesBundle.Key.Type;
        Key[] entityLinks = entityMatchesBundle.Meta.Links;

        Span<WordCompareResult> wordsScores = stackalloc WordCompareResult[context.SplittedAndNormalizedQuery.Length];

        //Считаем количество всех совпадений в найденной сущности и заполняем wordsScores
        foreach ((byte Type, List<WordCompareResult> Matches) in GetMatches(context, entityMatchesBundle))
        {
            double nodeMultipler = GetLinkedEntityMatchMultipler(currentEntityType, Type);
            ProcessNodeScoring(in wordsScores, Matches, context, nodeMultipler);
        }

        int resultScore = 0;

        //Складывам совпадения по словам из запроса
        foreach (WordCompareResult ws in wordsScores)
            resultScore += ws.Score;

        entityMatchesBundle.Score = resultScore;
    }

    private IEnumerable<(byte Type, List<WordCompareResult> Matches)> GetMatches(TContext context, EntitySearchResult entityMatchesBundle)
    {
        byte currentEntityType = entityMatchesBundle.Key.Type;

        BitArray256 matchedTypes = new();

        yield return (currentEntityType, entityMatchesBundle.WordsMatches);
        matchedTypes[currentEntityType] = true;

        foreach (Key nodeKey in entityMatchesBundle.Meta.Links)
        {
            if (matchedTypes[nodeKey.Type]
                || !context.ContainsEntity(nodeKey, out EntitySearchResult? chainedMath))
                continue;

            yield return (nodeKey.Type, chainedMath.WordsMatches);
            matchedTypes[nodeKey.Type] = true;

            //Пробуем провалится на уровень выше по условию и просчитать матчи с данного уровня
            if (OnLinkedMatchNeedMergeLinks(currentEntityType, nodeKey.Type))
            {
                Key[] chainedEntityLinks = chainedMath.Meta.Links;

                foreach (Key chainedEntityLink in chainedEntityLinks)
                {
                    if (matchedTypes[chainedEntityLink.Type]
                        || !context.ContainsEntity(chainedEntityLink, out EntitySearchResult? parentLinkMathes))
                        continue;

                    yield return (chainedEntityLink.Type, parentLinkMathes.WordsMatches);
                    matchedTypes[chainedEntityLink.Type] = true;
                }
            }
        }
    }

    private void ProcessNodeScoring(in Span<WordCompareResult> wordsScores, List<WordCompareResult> Matches, TContext context, double nodeMultipler)
    {
        Span<WordCompareResult> nodeScores = stackalloc WordCompareResult[wordsScores.Length];

        for (int i = 0; i < Matches.Count; i++)
        {
            WordCompareResult compareResult = Matches[i];
            WordCompareResult previousMatch = nodeScores[compareResult.WordBundlePosition];

            int queryWordPosition = -1;

            if (!previousMatch.IsEmpty)
            {
                if (previousMatch.NameType == compareResult.NameType && previousMatch.NameWordPosition != compareResult.NameWordPosition)
                {
                    queryWordPosition = GetEmptyQueryWordPosition(nodeScores, compareResult.WordBundlePosition);
                }
                else if (previousMatch.NameType != compareResult.NameType && previousMatch.NameWordPosition == compareResult.NameWordPosition)
                {
                    queryWordPosition = previousMatch.WordBundlePosition;
                }
            }
            else
            {
                queryWordPosition = GetEmptyQueryWordPosition(nodeScores, compareResult.WordBundlePosition);
            }

            if (queryWordPosition == -1) continue;

            double nameTypeMultipler = GetNameMultiplerInternal(compareResult.NameType);

            byte score = (byte)(compareResult.Score * nameTypeMultipler * nodeMultipler);

            if (previousMatch.Score < score)
                nodeScores[queryWordPosition] = new(compareResult.NameWordPosition, compareResult.NameType, compareResult.WordBundlePosition, score);
        }

        for (int i = 0; i < wordsScores.Length; i++)
        {
            WordCompareResult queryMatch = wordsScores[i];
            WordCompareResult nodeMatch = nodeScores[i];

            if (nodeMatch.IsEmpty) continue;
            if (!queryMatch.IsEmpty)
            {
                int nextQueryWordPosition = GetEmptyQueryWordPosition(wordsScores, i);
                if (nextQueryWordPosition != -1) wordsScores[nextQueryWordPosition] = nodeMatch;
            }
            else
            {
                wordsScores[i] = nodeMatch;
            }
        }

        int GetEmptyQueryWordPosition(in Span<WordCompareResult> scores, int wordBundlePosition)
        {
            foreach (int position in context.SearchWordsBundle[wordBundlePosition].PositionsInRequest)
            {
                if (scores[position].IsEmpty) return position;
            }

            return -1;
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

    internal double GetNameMultiplerInternal(byte nameType)
    {
        if (nameType == 0)
            return 1;

        return GetNameTypeMultipler(nameType);
    }
    #endregion

}