using QuerySeek.Models;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching.Requests;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Позволяет определить стратегию поиска
/// </summary>
/// <typeparam name="TContext"></typeparam>
/// <param name="splitter"></param>
/// <param name="normalizer"></param>
public abstract class SearcherBase<TContext>(IPhraseSplitter splitter, INormalizer normalizer) where TContext : SearchContextBase
{
    #region Search logic
    /// <summary>
    /// Поиск топа всех типов
    /// </summary>
    /// <param name="context"></param>
    /// <param name="take"></param>
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

        return PostProcessing(context, GetAllResults()
            .OrderByDescending(i =>
            {
                i.Score = CalculateScore(context, i);
                return i.Score;
            }))
            .Take(take)
            .ToArray();

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
    /// <param name="context"></param>
    /// <param name="selectTypes"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public TypeSearchResult[] SearchTypes(
        TContext context,
        string query,
        (byte Type, int Take)[] selectTypes,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        FillContext(context, query);

        foreach (RequestBase i in context.Request) i.ProcessRequest(context, ct);

        var result = new TypeSearchResult[selectTypes.Length];

        for (int i = 0; i < selectTypes.Length; i++)
        {
            (byte Type, int Take) = selectTypes[i];
            Dictionary<Key, EntitySearchResult>? typeSearchResult = context.GetResultsByType(Type);

            if (typeSearchResult is null)
            {
                result[i] = new(Type, []);
                continue;
            }

            EntitySearchResult[] typeResult =
                PostProcessing(context, TypeBundlePreprocessing(context, Type, typeSearchResult.Values)
                    .OrderByDescending(matchBundle =>
                    {
                        matchBundle.Score = CalculateScore(context, matchBundle);
                        return matchBundle.Score;
                    })
                )
                .Take(Take)
                .ToArray();

            result[i] = new(Type, typeResult);
        }

        return result;
    }

    public void FillContext(TContext context, string query)
    {
        context.Query = query;

        string[] splittedQuery = TextPreprocessor.PreprocessPhrase(splitter, normalizer, query);

        Dictionary<string, string[]> alternativeWords = GetWordsAlternativesPairs();
        Dictionary<string, double> queryWordMultiplers = GetQueryWordsMultiplers();

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

        context.NgrammedQuery = ngrammedWords;
        context.SplittedAndNormalizedQuery = splittedQuery;
        context.Request = GetRequest(context);
        context.WordsSearchSettings = GetWordsSearchSettings(context);
        context.SearchWordsBundle = SearchSimlarIndexWordsByQuery(context);
    }

    public List<KeyValuePair<int, byte>>[] SearchSimlarIndexWordsByQuery(SearchContextBase searchContext)
    {
        QueryWordContainer[] splittedQuery = searchContext.NgrammedQuery;
        WordsSearchSettings wordsSearchSettings = searchContext.WordsSearchSettings;
        Dictionary<int, int[]> wordsIdsByNgramms = searchContext.Index.WordsIdsByNgramms;

        var result = new List<KeyValuePair<int, byte>>[splittedQuery.Length];

        //Используем один словарь для расчета совпавщих слов для каждого слова из запроса дабы лишний раз не аллоцировать
        Dictionary<int, WordNgrammSearchState> wordsSearchProcessDict = new(wordsSearchSettings.WordsSearchDictionaryPreallocate);

        for (int i = 0; i < result.Length; i++)
        {
            QueryWordContainer currentWord = splittedQuery[i];

            //Проверка на введеное слово ранее, чтоб не повторять вычисления
            for (int j = i - 1; j >= 0; j--)
            {
                if (splittedQuery[j].QueryWord.Equals(currentWord.QueryWord))
                {
                    result[i] = result[j];
                    break;
                }
            }

            if (result[i] is null)
            {
                result[i] = SearchSimilarsByQueryWordAndAlternatives(
                    wordsSearchProcessDict,
                    wordsIdsByNgramms,
                    currentWord,
                    wordsSearchSettings);
            }
        }

        return result;
    }

    private static List<KeyValuePair<int, byte>> SearchSimilarsByQueryWordAndAlternatives(
        Dictionary<int, WordNgrammSearchState> wordsSearchProcessDict,
        Dictionary<int, int[]> wordsIdsByNgramms,
        QueryWordContainer wordContainer,
        WordsSearchSettings wordsSearchSettings)
    {
        List<KeyValuePair<int, byte>> result = [];

        //Ищем по одной четкой алтернативе
        foreach (Word altWord in wordContainer.Alternatives)
            SearchSimilars(wordsSearchProcessDict, altWord, (byte)wordContainer.QueryWord.NGrammsHashes.Length);

        int treshold = wordContainer.QueryWord.IsDigit
            ? wordContainer.QueryWord.NGrammsHashes.Length - Ngramms.NGRAM_LENGTH + 1
            : (int)(wordContainer.QueryWord.NGrammsHashes.Length * wordsSearchSettings.Similarity);

        SearchSimilars(wordsSearchProcessDict, wordContainer.QueryWord, treshold);

        return result;

        void SearchSimilars(Dictionary<int, WordNgrammSearchState> wordsSearchProcessDict, Word queryWord, int treshold)
        {
            Ngramms.NgrammSearch(wordsSearchProcessDict, wordsIdsByNgramms, queryWord, treshold);

            //Ищем бандл схожих слов и сортируем по количеству совпадений (вычисляется в свойстве Score. Попадания - наказание за промахи)
            foreach (KeyValuePair<int, WordNgrammSearchState> item in wordsSearchProcessDict
                .Where(i => i.Value.Score >= treshold)
                .OrderByDescending(i => i.Value.Score)
                .Take(wordsSearchSettings.MaxCheckingWordsCount))
            {
                result.Add(new(item.Key, (byte)(item.Value.Score * wordContainer.Multipler)));
            }

            //Чистка переиспользуемого словаря
            wordsSearchProcessDict.Clear();
        }
    }

    public int CalculateScore(TContext searchContext, EntitySearchResult entityMatchesBundle)
    {
        byte currentEntityType = entityMatchesBundle.Key.Type;
        Key[] entityLinks = entityMatchesBundle.Meta.Links;

        Span<int> wordsScores = stackalloc int[searchContext.NgrammedQuery.Length];

        //Считаем количество всех совпадений в найденной сущности и заполняем wordsScores
        CalculateEntityPartScore(in wordsScores, entityMatchesBundle.WordsMatches, 1);

        //Добавление матчей из связанных сущностей если они найдены в контексте
        foreach (Key nodeKey in entityLinks)
        {
            if (searchContext.GetResultsByType(nodeKey.Type) is { } req
                && req.TryGetValue(nodeKey, out EntitySearchResult? chainedMathes))
            {
                double nodeMultipler = GetLinkedEntityMatchMultipler(currentEntityType, nodeKey.Type);
                CalculateEntityPartScore(in wordsScores, chainedMathes.WordsMatches, nodeMultipler);
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

        return resultScore;
    }

    private void CalculateEntityPartScore(
        in Span<int> wordsScores,
        List<WordCompareResult> wordsMatches,
        double nodeMultipler)
    {
        //TODO: тут надо хорошо подумать как получше дистинктить слова
        foreach (WordCompareResult compareResult in wordsMatches)
        {
            int score = compareResult.MatchLength;

            int queryWordPosition = compareResult.QueryWordPosition;
            double phraseMultipler = GetPhraseMultiplerInternal(compareResult.PhraseType);

            score = (int)(score * phraseMultipler * nodeMultipler);

            if (wordsScores[queryWordPosition] < score)
                wordsScores[queryWordPosition] = score;
        }
    }

    internal double GetPhraseMultiplerInternal(byte phraseType)
    {
        if (phraseType == 0)
            return 1;

        return GetPhraseTypeMultipler(phraseType);
    }
    #endregion

    #region Overrides
    /// <summary>
    /// Определяет запрос на поиск в индексе - что ищем в индексе
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public abstract RequestBase[] GetRequest(TContext context);

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
    /// Множитель совпадений из связанных сущностей
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="linkedType"></param>
    /// <returns></returns>
    public virtual double GetLinkedEntityMatchMultipler(byte entityType, byte linkedType)
        => 1;

    /// <summary>
    /// Множитель типа фразы
    /// </summary>
    /// <param name="phraseType"></param>
    /// <returns></returns>
    public virtual double GetPhraseTypeMultipler(byte phraseType)
        => 1;

    /// <summary>
    /// Определение настроек поиска по словам
    /// </summary>
    /// <param name="searchContext"></param>
    /// <returns></returns>
    public virtual WordsSearchSettings GetWordsSearchSettings(TContext searchContext)
        => searchContext.NgrammedQuery.Length > 5
            ? WordsSearchSettings.Fast
            : WordsSearchSettings.Default;

    /// <summary>
    /// Определяем возможные альтернативные слова для слов из запроса
    /// </summary>
    /// <returns></returns>
    public virtual Dictionary<string, string[]> GetWordsAlternativesPairs()
        => [];

    /// <summary>
    /// Определяем моножители для слов из запроса (можем уменьшать значимость предлогов и тд)
    /// </summary>
    /// <returns></returns>
    public virtual Dictionary<string, double> GetQueryWordsMultiplers()
        => [];

    #endregion
}

public record QueryWordContainer(Word QueryWord, Word[] Alternatives, double Multipler);