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
        => context.NgrammedQuery.Length > 5
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

        foreach (RequestBase i in context.Request) i.ProcessRequest(context, ct);

        return
        [..
            PostProcessing(context, GetAllResults().OrderByDescending(i =>
                {
                    CalculateScore(context, i);
                    return i.Score;
                })
            )
            .Take(take)
        ];

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

        foreach (RequestBase i in context.Request) i.ProcessRequest(context, ct);

        TypeSearchResult[] result = [.. context.SearchResult.Select(typeSearchResult =>
        {
            byte currentType = typeSearchResult.Key;

            EntitySearchResult[] typeResult =
            [..
                PostProcessing(context, TypeBundlePreprocessing(context, currentType, typeSearchResult.Value.Values)
                    .OrderByDescending(matchBundle =>
                    {
                        CalculateScore(context, matchBundle);
                        return matchBundle.Score;
                    })
                )
                .Take(take)
            ];

            return new TypeSearchResult(currentType, typeResult);
        })];

        return result;
    }

    public void FillContext(TContext context, string query)
    {
        string[] splittedQuery = TextPreprocessor.PreprocessName(nameTokenizer, normalizer, query);

        Dictionary<string, string[]> alternativeWords = GetWordsAlternativesPairs(context);
        Dictionary<string, double> queryWordMultiplers = GetQueryWordsMultiplers(context);

        QueryWordContainer[] ngrammedWords = Array.ConvertAll(splittedQuery, word =>
        {
            double multipler = queryWordMultiplers.TryGetValue(word, out var m) ? m : 1;

            Word[] alterantivesMetas = [];

            if (alternativeWords.TryGetValue(word, out string[]? alternatives))
                alterantivesMetas = Array.ConvertAll(alternatives, alt => new Word(normalizer.Normalize(alt)));

            return new QueryWordContainer(
                new Word(word),
                alterantivesMetas,
                multipler);
        });

        context.Query = query;
        context.SplittedAndNormalizedQuery = splittedQuery;
        context.NgrammedQuery = ngrammedWords;
        context.WordsSearchSettings = GetWordsSearchSettings(context);
        context.SearchWordsBundle = NgrammsWordsSearchHelper.SearchSimlarIndexWordsByQuery(
            context.NgrammedQuery,
            context.WordsSearchSettings,
            context.Index.WordsIdsByNgramms);
        context.Request = GetRequest(context);
    }

    public void CalculateScore(TContext searchContext, EntitySearchResult entityMatchesBundle)
    {
        byte currentEntityType = entityMatchesBundle.Key.Type;
        Key[] entityLinks = entityMatchesBundle.Meta.Links;

        StackBitArray matchedTypes = new(stackalloc int[8]);
        Span<byte> wordsScores = stackalloc byte[searchContext.NgrammedQuery.Length];

        //Считаем количество всех совпадений в найденной сущности и заполняем wordsScores
        double multipler = GetLinkedEntityMatchMultipler(currentEntityType, currentEntityType);
        CalculateEntityPartScore(in wordsScores, entityMatchesBundle.WordsMatches, multipler);

        //Добавление матчей из связанных сущностей если они найдены в контексте
        foreach (Key nodeKey in entityLinks)
        {
            double nodeMultipler;

            if (matchedTypes.Get(nodeKey.Type)
                || (nodeMultipler = GetLinkedEntityMatchMultipler(currentEntityType, nodeKey.Type)) == 0
                || !searchContext.ContainsEntity(nodeKey, out EntitySearchResult? chainedMath))
                continue;
            
            CalculateEntityPartScore(in wordsScores, chainedMath.WordsMatches, nodeMultipler);
            matchedTypes.Set(nodeKey.Type, true);

            //Пробуем провалится на уровень выше по условию и просчитать матчи с данного уровня
            if (OnLinkedMatchNeedMergeLinks(currentEntityType, nodeKey.Type))
            {
                Key[] chainedEntityLinks = chainedMath.Meta.Links;

                foreach (Key chainedEntityLink in chainedEntityLinks)
                {
                    if (matchedTypes.Get(chainedEntityLink.Type)
                        || (nodeMultipler = GetLinkedEntityMatchMultipler(currentEntityType, chainedEntityLink.Type)) == 0
                        || !searchContext.ContainsEntity(chainedEntityLink, out EntitySearchResult? parentLinkMathes))
                        continue;

                    CalculateEntityPartScore(in wordsScores, parentLinkMathes.WordsMatches, nodeMultipler);
                    matchedTypes.Set(chainedEntityLink.Type, true);
                }
            }
        }

        int resultScore = 0;

        //Складывам совпадения по словам из запроса
        foreach (int ws in wordsScores)
            resultScore += ws;

        //Обрабатываем дополнительные правила
        for (int i = 0; i < entityMatchesBundle.Rules.Count; i++)
        {
            AdditionalRule item = entityMatchesBundle.Rules[i];

            resultScore += item.Score;
            resultScore = (int)(resultScore * item.Multipler);
        }

        //Записываем итоговый скор
        entityMatchesBundle.Score = resultScore;
    }

    private void CalculateEntityPartScore(
        in Span<byte> wordsScores,
        List<WordCompareResult> wordsMatches,
        double nodeMultipler)
    {
        //TODO: тут надо хорошо подумать как получше дистинктить слова
        for (int i = 0; i < wordsMatches.Count; i++)
        {
            WordCompareResult compareResult = wordsMatches[i];
            byte score = compareResult.MatchLength;

            int queryWordPosition = compareResult.QueryWordPosition;
            double nameTypeMultipler = GetNameMultiplerInternal(compareResult.NameType);

            score = (byte)(score * nameTypeMultipler * nodeMultipler);

            if (wordsScores[queryWordPosition] < score)
                wordsScores[queryWordPosition] = score;
        }
    }

    internal double GetNameMultiplerInternal(byte nameType)
    {
        if (nameType == 0)
            return 1;

        return GetNameTypeMultipler(nameType);
    }
    #endregion

}