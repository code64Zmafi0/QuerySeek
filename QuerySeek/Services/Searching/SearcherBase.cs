using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuerySeek.Models;
using QuerySeek.Services.Extensions;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Splitting;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Поисковик, позволяет переопределить сортировку.
/// </summary>
/// <typeparam name="TContext"></typeparam>
/// <param name="splitter"></param>
/// <param name="normalizer"></param>
public class SearcherBase<TContext>(IPhraseSplitter splitter, INormalizer normalizer) where TContext : SearchContextBase
{
    #region Search logic
    /// <summary>
    /// Поиск топа всех типов
    /// </summary>
    /// <param name="context"></param>
    /// <param name="take"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public EntityMatchesBundle[] Search(
        TContext context,
        int take,
        CancellationToken? cancellationToken = null)
    {
        var ct = cancellationToken ?? new CancellationTokenSource(TimeoutMs).Token;

        FillContext(context);

        PerfomanceSettings perfomance = GetPerfomance(context);

        List<KeyValuePair<int, byte>>[] wordsBundle = SearchSimlarIndexWordsByQuery(context, perfomance);

        foreach (var i in context.Request) i.ProcessRequest(context, wordsBundle, perfomance, ct);

        return PostProcessing(context, GetAllResults()
            .OrderByDescending(i =>
            {
                i.Score = CalculateScore(context, i);
                return i.Score;
            }))
            .Take(take)
            .ToArray();

        IEnumerable<EntityMatchesBundle> GetAllResults()
        {
            foreach (var typeResults in context.SearchResult)
            {
                foreach (var item in ResultVisionFilter(context, typeResults.Key, typeResults.Value.Values))
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

        PerfomanceSettings perfomance = GetPerfomance(context);

        List<KeyValuePair<int, byte>>[] wordsBundle = SearchSimlarIndexWordsByQuery(context, perfomance);

        foreach (var i in context.Request) i.ProcessRequest(context, wordsBundle, perfomance, ct);

        var result = new TypeSearchResult[selectTypes.Length];

        for (int i = 0; i < selectTypes.Length; i++)
        {
            (byte Type, int Take) = selectTypes[i];
            Dictionary<Key, EntityMatchesBundle>? typeSearchResult = context.GetResultsByType(Type);

            if (typeSearchResult is null)
            {
                result[i] = new(Type, []);
                continue;
            }

            var typeResult =
                PostProcessing(context, ResultVisionFilter(context, Type, typeSearchResult.Values)
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

    private void FillContext(TContext context)
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
    }    

    private List<KeyValuePair<int, byte>>[] SearchSimlarIndexWordsByQuery(SearchContextBase searchContext, PerfomanceSettings perfomance)
    {
        var splittedQuery = searchContext.NgrammedQuery;
        var result = new List<KeyValuePair<int, byte>>[splittedQuery.Length];

        //Используем один словарь для расчета совпавщих нграмм для каждого слова дабы лишний раз не аллоцировать
        Dictionary<int, IndexWordSearchInfo> wordsSearchProcessDict = new(perfomance.WordsSearchDictionaryPreallocate);

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
                    perfomance,
                    wordsSearchProcessDict);
            }
        }

        return result;
    }

    private List<KeyValuePair<int, byte>> SearchSimilarWordByQueryAndAlternatives(
        IndexInstance index,
        QueryWordContainer wordContainer,
        PerfomanceSettings perfomance,
        Dictionary<int, IndexWordSearchInfo> wordsSearchProcessDict)
    {
        List<KeyValuePair<int, byte>> result = [];

        //Ищем по одной четкой алтернативе
        foreach (Word altWord in wordContainer.Alternatives)
            SearchSimilars(altWord, (byte)wordContainer.QueryWord.NGrammsHashes.Length, wordsSearchProcessDict);

        int treshold = wordContainer.QueryWord.IsDigit
            ? wordContainer.QueryWord.NGrammsHashes.Length - QS.NGRAM_LENGTH + 1
            : (int)(wordContainer.QueryWord.NGrammsHashes.Length * SimilarityTreshold);

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
                .Take(perfomance.MaxCheckingWordsCount))
            {
                result.Add(new(item.Key, item.Value.Score));
            }

            //Чистка переиспользуемого словаря
            wordsSearchProcessDict.Clear();
        }
    }

    /// <summary>
    /// Метод отвечает за поиск похожих слов по 2-gramm
    /// </summary>
    /// <returns>Словарь id слова, количество свопадений и пропусков</returns>
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

    private int CalculateScore(TContext searchContext, EntityMatchesBundle entityMatchesBundle)
    {
        Key currentEntityKey = entityMatchesBundle.Key;
        Span<int> wordsScores = stackalloc int[searchContext.NgrammedQuery.Length];

        //Считаем количество всех совпадений в найденной сущности и заполняем wordsScores
        CalculateNodeMatchesScore(in wordsScores, entityMatchesBundle.WordsMatches, 1);

        //Добавление матчей из связанных сущностей если они найдены в контексте
        Key[] nodes = entityMatchesBundle.EntityMeta.Links;
        foreach (Key nodeKey in nodes)
        {
            if (searchContext.GetResultsByType(nodeKey.Type) is { } req
                && req.TryGetValue(nodeKey, out var chaiedMathes))
            {
                double nodeMultipler = GetLinkedEntityMatchMiltipler(currentEntityKey.Type, nodeKey.Type);
                CalculateNodeMatchesScore(in wordsScores, chaiedMathes.WordsMatches, nodeMultipler);

                if (OnLinkedEntityMatched(currentEntityKey, nodeKey) is { } chainedMatchRule)
                    entityMatchesBundle.Rules.Add(chainedMatchRule);
            }
        }

        if (OnEntityProcessed(searchContext, entityMatchesBundle) is { } rule)
            entityMatchesBundle.Rules.Add(rule);

        int resultScore = 0;

        //Складывам совпадения по словам из запроса
        foreach (int ws in wordsScores)
            resultScore += ws;

        //Обрабатываем дополнительные правила
        resultScore += entityMatchesBundle.RulesScore;
        foreach (AdditionalRule item in entityMatchesBundle.Rules)
        {
            resultScore = (int)(resultScore * item.Multipler);
        }

        return resultScore;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateNodeMatchesScore(
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
    /// Позволяет переопределить сортировку
    /// </summary>
    /// <param name="context"></param>
    /// <param name="result">Отсортированный по количеству совпадений enumerable сущностей</param>
    /// <returns></returns>
    public virtual IOrderedEnumerable<EntityMatchesBundle> PostProcessing(TContext context, IOrderedEnumerable<EntityMatchesBundle> result)
        => result;

    /// <summary>
    /// Позволяет отсортировать показ определенных типов
    /// </summary>
    /// <param name="context"></param>
    /// <param name="type"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public virtual IEnumerable<EntityMatchesBundle> ResultVisionFilter(TContext context, byte type, IEnumerable<EntityMatchesBundle> result)
        => result;

    /// <summary>
    /// Множитель сопадений из связанных сущностей
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="linkedType"></param>
    /// <returns></returns>
    public virtual double GetLinkedEntityMatchMiltipler(byte entityType, byte linkedType)
        => 1;

    /// <summary>
    /// Множитель типа фразы
    /// </summary>
    /// <param name="phraseType"></param>
    /// <returns></returns>
    public virtual double GetPhraseTypeMultipler(byte phraseType)
        => 1;

    /// <summary>
    /// Добавление правила если линк совпал
    /// </summary>
    /// <param name="entityKey"></param>
    /// <param name="linkedKey"></param>
    /// <returns></returns>
    public virtual AdditionalRule? OnLinkedEntityMatched(Key entityKey, Key linkedKey)
        => null;

    /// <summary>
    /// Добавление правила на совпадение сущности
    /// </summary>
    /// <param name="context"></param>
    /// <param name="entityMatchesBundle"></param>
    /// <returns></returns>
    public virtual AdditionalRule? OnEntityProcessed(TContext context, EntityMatchesBundle entityMatchesBundle)
        => null;

    /// <summary>
    /// Таймаут если не передан ct
    /// </summary>
    public virtual int TimeoutMs
        => 1500;

    /// <summary>
    /// Трешхолд совпадения слов
    /// </summary>
    public virtual double SimilarityTreshold
        => 0.5;

    /// <summary>
    /// Определение настроек перфоманса
    /// </summary>
    /// <param name="searchContext"></param>
    /// <returns></returns>
    public virtual PerfomanceSettings GetPerfomance(SearchContextBase searchContext)
        => searchContext.NgrammedQuery.Length > 5
            ? PerfomanceSettings.Fast
            : PerfomanceSettings.Default;

    #endregion
}

public record QueryWordContainer(Word QueryWord, Word[] Alternatives, bool NotRealivated);
