using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuerySeek.Models;
using QuerySeek.Services.Extensions;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching.Requests;
using QuerySeek.Services.Splitting;

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
        int take,
        CancellationToken? cancellationToken = null)
    {
        var ct = cancellationToken ?? new CancellationTokenSource(TimeoutMs).Token;

        FillContext(context);

        WordsSearchSettings wordsSearchSettings = GetWordsSearchSettings(context);

        List<KeyValuePair<int, byte>>[] wordsBundle = SearchSimlarIndexWordsByQuery(context, wordsSearchSettings);

        foreach (var i in context.Request) i.ProcessRequest(context, wordsBundle, wordsSearchSettings, ct);

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
            foreach (var typeResults in context.SearchResult)
            {
                foreach (var item in TypeBundlePreprocessing(context, typeResults.Key, typeResults.Value.Values))
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
        (byte Type, int Take)[] selectTypes,
        CancellationToken? cancellationToken = null)
    {
        var ct = cancellationToken ?? new CancellationTokenSource(TimeoutMs).Token;

        FillContext(context);

        WordsSearchSettings wordsSearchSettings = GetWordsSearchSettings(context);

        List<KeyValuePair<int, byte>>[] wordsBundle = SearchSimlarIndexWordsByQuery(context, wordsSearchSettings);

        foreach (var i in context.Request) i.ProcessRequest(context, wordsBundle, wordsSearchSettings, ct);

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

            var typeResult =
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

    public void FillContext(TContext context)
    {
        string normalizedQuery = normalizer.Normalize(context.Query);
        string[] splittedQuery = splitter.Tokenize(normalizedQuery);

        QueryWordContainer[] ngrammedWords = Array.ConvertAll(splittedQuery, i =>
        {
            bool notRealivated = context.NotRealivatedWords.Contains(i);

            Word[] alterantivesMetas = [];

            if (context.AlternativeWords.TryGetValue(i, out var alternatives))
                alterantivesMetas = Array.ConvertAll(alternatives, alt => new Word(normalizer.Normalize(alt)));

            return new QueryWordContainer(
                new Word(i),
                alterantivesMetas,
                notRealivated);
        });

        context.NgrammedQuery = ngrammedWords;
        context.SplittedAndNormalizedQuery = splittedQuery;
        context.Request = GetRequest(context);
    }    

    public List<KeyValuePair<int, byte>>[] SearchSimlarIndexWordsByQuery(SearchContextBase searchContext, WordsSearchSettings wordsSearchSettings)
    {
        var splittedQuery = searchContext.NgrammedQuery;
        var result = new List<KeyValuePair<int, byte>>[splittedQuery.Length];

        //Используем один словарь для расчета совпавщих нграмм для каждого слова дабы лишний раз не аллоцировать
        Dictionary<int, IndexWordSearchInfo> wordsSearchProcessDict = new(wordsSearchSettings.WordsSearchDictionaryPreallocate);

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
                result[i] = SearchSimilarWordByQueryAndAlternatives(
                    searchContext.Index,
                    currentWord,
                    wordsSearchSettings,
                    wordsSearchProcessDict);
            }
        }

        return result;
    }

    private static List<KeyValuePair<int, byte>> SearchSimilarWordByQueryAndAlternatives(
        IndexInstance index,
        QueryWordContainer wordContainer,
        WordsSearchSettings wordsSearchSettings,
        Dictionary<int, IndexWordSearchInfo> wordsSearchProcessDict)
    {
        List<KeyValuePair<int, byte>> result = [];

        //Ищем по одной четкой алтернативе
        foreach (Word altWord in wordContainer.Alternatives)
            SearchSimilars(altWord, (byte)wordContainer.QueryWord.NGrammsHashes.Length, wordsSearchProcessDict);

        int treshold = wordContainer.QueryWord.IsDigit
            ? wordContainer.QueryWord.NGrammsHashes.Length - QS.NGRAM_LENGTH + 1
            : (int)(wordContainer.QueryWord.NGrammsHashes.Length * wordsSearchSettings.Similarity);

        SearchSimilars(wordContainer.QueryWord, treshold, wordsSearchProcessDict);

        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SearchSimilars(Word queryWord, int treshold, Dictionary<int, IndexWordSearchInfo> wordsSearchProcessDict)
        {
            int queryLength = queryWord.NGrammsHashes.Length;

            Dictionary<int, IndexWordSearchInfo> similars = GetSimilarWords(index, queryWord, treshold, wordsSearchProcessDict);

            //Ищем бандл схожих слов и сортируем по количеству совпадений (вычисляется в свойстве Score. Попадания - наказание за промахи)
            foreach (KeyValuePair<int, IndexWordSearchInfo> item in similars
                .Where(i => i.Value.Score >= treshold)
                .OrderByDescending(i => i.Value.Score)
                .Take(wordsSearchSettings.MaxCheckingWordsCount))
            {
                result.Add(new(item.Key, item.Value.Score));
            }

            //Чистка переиспользуемого словаря
            wordsSearchProcessDict.Clear();
        }
    }

    /// <summary>
    /// Метод отвечает за поиск похожих слов по n-gramm
    /// </summary>
    /// <returns>Словарь id слова количество совпадений и пропусков</returns>
    private static Dictionary<int, IndexWordSearchInfo> GetSimilarWords(
        IndexInstance index,
        Word queryWord,
        int treshold,
        Dictionary<int, IndexWordSearchInfo> wordsSearchProcessDict)
    {
        byte wordLength = (byte)queryWord.NGrammsHashes.Length;

        Dictionary<int, IndexWordSearchInfo> words = wordsSearchProcessDict;

        //Ищем в индексе слов, считаем совпавшие ngramm-ы и пропуски
        for (byte queryWordNgrammIndex = 0; queryWordNgrammIndex < wordLength; queryWordNgrammIndex++)
        {
            if (!index.WordsIdsByNgramms.TryGetValue(queryWord.NGrammsHashes[queryWordNgrammIndex], out int[]? wordsIds))
                continue;

            foreach (int wordId in wordsIds)
            {
                ref var matchInfo = ref CollectionsMarshal.GetValueRefOrNullRef(words, wordId);

                if (!Unsafe.IsNullRef(ref matchInfo))
                {
                    matchInfo = new()
                    {
                        Mathes = (byte)(matchInfo.Mathes + 1),
                        Misses = CalculateMiss(in matchInfo, queryWordNgrammIndex),
                        PreviousMatch = queryWordNgrammIndex,
                    };

                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    static byte CalculateMiss(in IndexWordSearchInfo compareFactor, int queryWordNgrammIndex)
                    {
                        if (queryWordNgrammIndex == 0) return 0;

                        byte missCount = (byte)(queryWordNgrammIndex - compareFactor.PreviousMatch - 1);

                        return (byte)(compareFactor.Misses + missCount);
                    }
                }
                //Попытка отбить добавление в словарь уже точно не совпавщих по treshold
                else if (queryWordNgrammIndex == 0 || (!queryWord.IsDigit && queryWordNgrammIndex <= treshold))
                    words[wordId] = new(1, 0, queryWordNgrammIndex);
            }
        }

        return words;
    }

    private int CalculateScore(TContext searchContext, EntitySearchResult entityMatchesBundle)
    {
        Key currentEntityKey = entityMatchesBundle.Key;
        Key[] entityLinks = searchContext.Index.Entities[currentEntityKey].Links;

        Span<int> wordsScores = stackalloc int[searchContext.NgrammedQuery.Length];

        //Считаем количество всех совпадений в найденной сущности и заполняем wordsScores
        CalculateEntityPartScore(in wordsScores, entityMatchesBundle.WordsMatches, 1);

        //Добавление матчей из связанных сущностей если они найдены в контексте
        foreach (Key nodeKey in entityLinks)
        {
            if (searchContext.GetResultsByType(nodeKey.Type) is { } req
                && req.TryGetValue(nodeKey, out var chaiedMathes))
            {
                double nodeMultipler = GetLinkedEntityMatchMultipler(currentEntityKey.Type, nodeKey.Type);
                CalculateEntityPartScore(in wordsScores, chaiedMathes.WordsMatches, nodeMultipler);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateEntityPartScore(
        in Span<int> wordsScores,
        List<WordCompareResult> wordsMatches,
        double nodeMultipler)
    {
        //TODO: тут надо хорошо подумать как получше дистинктить слова
        for (int wordMatchIndex = 0; wordMatchIndex < wordsMatches.Count; wordMatchIndex++)
        {
            WordCompareResult compareResult = wordsMatches[wordMatchIndex];

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
    /// Таймаут если не передан ct
    /// </summary>
    public virtual int TimeoutMs
        => 1500;

    /// <summary>
    /// Определение настроек поиска по словам
    /// </summary>
    /// <param name="searchContext"></param>
    /// <returns></returns>
    public virtual WordsSearchSettings GetWordsSearchSettings(SearchContextBase searchContext)
        => searchContext.NgrammedQuery.Length > 5
            ? WordsSearchSettings.Fast
            : WordsSearchSettings.Default;

    #endregion
}

public record QueryWordContainer(Word QueryWord, Word[] Alternatives, bool NotRealivated);
