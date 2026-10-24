using QuerySeek.Models;
using QuerySeek.Services.Helpers;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching.Requests;
using QuerySeek.Services.Searching.Scoring;
using QuerySeek.Services.Searching.WordsSearch;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Процессор поиска
/// </summary>
/// <typeparam name="TContext">Тип конеткса поиска</typeparam>
/// <param name="normalizer">Нормалайзер запроса</param>
/// <param name="tokenizer">Токенайзер запроса</param>
/// <param name="scoreCalculator">Калькулятор очков для ранжирования</param>
public abstract class SearcherBase<TContext>(
    INormalizer normalizer,
    ITokenizer tokenizer,
    IScoreCalculator<TContext> scoreCalculator) where TContext : SearchContextBase
{
    #region Overrides
    /// <summary>
    /// (0. WORDS SEARCH) Определение настроек поиска схожих слов для слов из запроса
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual WordsSearchSettings GetWordsSearchSettings(TContext context)
        => WordsSearchSettings.Default;

    /// <summary>
    /// (1. SELECTION) Определяет запрос на поиск сущностей в индексе
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <returns>Перечисление запросов вниз по дереву зависимостей</returns>
    public abstract IEnumerable<EntitiesSelectionOperation> GetRequest(TContext context);

    /// <summary>
    /// (2. PREPROCESSING) Позволяет осуществить фильтрацию выборки по типам перед сортировкой или обогатить правилами
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="type">Текущий тип сущностей</param>
    /// <param name="result">Выборка сущностей данного типа</param>
    public virtual IEnumerable<EntitySearchResult> TypeResultPreprocessing(TContext context, byte type, ICollection<EntitySearchResult> result)
        => result;

    /// <summary>
    /// (3. RANGING) Определяет сортировку
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="result">Выборка сущностей для сортировки</param>
    public virtual IEnumerable<EntitySearchResult> Ranging(TContext context, IEnumerable<EntitySearchResult> result)
        => result.OrderByDescending(i => i.ScoreWithRules);
    #endregion

    /// <summary>
    /// Поиск топа всех типов
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="take">Количество элементов</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Отранжированный результат из всех типов</returns>
    public EntitySearchResult[] Search(
        TContext context,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        PrepareContext(context);
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
    /// <param name="context">Поисковой контекст</param>
    /// <param name="take">Количество элементов каждого типа</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Блоки отранжированных результатов по типам</returns>
    public TypeSearchResult[] SearchTypes(
        TContext context,
        int take,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        PrepareContext(context);
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

    public void PrepareContext(TContext context)
    {
        context.Normalizer = normalizer;
        context.Tokenizer = tokenizer;
        context.SplittedAndNormalizedQuery = TextPreprocessor.PreprocessText(tokenizer, normalizer, context.Query);
        context.WordsSearchSettings = GetWordsSearchSettings(context);
        context.SearchWordsBundle = NgrammsWordsSearcher.CreateSearchWordsBundle(
            context.SplittedAndNormalizedQuery,
            context.Index.WordsIdsByNgramms,
            context.WordsSearchSettings);
    }

    public void ProcessRequests(TContext context, CancellationToken ct)
    {
        foreach (EntitiesSelectionOperation request in GetRequest(context))
        {
            request.Process(context, ct);

            if (!context.TryGetResultsByType(request.TargetType, out Dictionary<Key, EntitySearchResult>? result))
                continue;

            foreach (EntitySearchResult item in result.Values)
                scoreCalculator.CalculateTextScore(context, item);
        }
    }
}
