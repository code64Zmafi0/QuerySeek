using QuerySeek.Models;
using QuerySeek.Services.Helpers;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching.Scoring;
using QuerySeek.Services.Searching.Selection;
using QuerySeek.Services.Searching.WordsSearch;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Процессор поиска
/// </summary>
/// <typeparam name="TContext">Тип контекста поиска</typeparam>
/// <typeparam name="TResult">Тип возвращаемого результата</typeparam>
/// <param name="normalizer">Нормалайзер запроса</param>
/// <param name="tokenizer">Токенайзер запроса</param>
/// <param name="scoreCalculator">Калькулятор очков для ранжирования</param>
public abstract class SearcherBase<TContext, TResult>(
    INormalizer normalizer,
    ITokenizer tokenizer,
    IScoreCalculator<TContext> scoreCalculator) 
    where TContext : SearchContextBase
    where TResult : class
{
    #region Overrides
    /// <summary>
    /// (1. WORDS SEARCH) Определение настроек поиска схожих слов для слов из запроса
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual WordsSearchSettings GetWordsSearchSettings(TContext context)
        => WordsSearchSettings.Default;

    /// <summary>
    /// (2. SELECTION) Определяет запрос на поиск/выборку сущностей в индексе
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <returns>Перечисление запросов вниз по дереву зависимостей</returns>
    public abstract IEnumerable<EntitiesSelectionOperation> GetRequest(TContext context);

    /// <summary>
    /// (3. RESULT BUILDING) Определяет формирование результата поиска
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public abstract TResult BuildResult(TContext context);

    /// <summary>
    /// Событие после подготовки контекста
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual void OnContextPreapred(TContext context) { }

    /// <summary>
    /// Событие после выполенния запроса
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    public virtual void OnRequestsProcessed(TContext context) { }
    #endregion

    /// <summary>
    /// Поиск топа всех типов
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Отранжированный результат из всех типов</returns>
    public TResult Search(
        TContext context,
        CancellationToken? cancellationToken = null)
    {
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        PrepareContext(context);
        OnContextPreapred(context);
        ProcessRequests(context, ct);
        OnRequestsProcessed(context);

        return BuildResult(context);
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
