using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching.PipelineSteps;
using QuerySeek.Services.Searching.Scoring;
using QuerySeek.Services.Searching.Selection;
using QuerySeek.Services.Searching.WordsSearch;

namespace QuerySeek.Services.Searching;

/// <summary>
/// Пайплаин поиска
/// </summary>
/// <typeparam name="TContext">Тип обрабатываемого контекста</typeparam>
public interface ISearchPipeline<TContext> where TContext : SearchContextBase
{
    TContext Execute(TContext context);
}

/// <summary>
/// Шаг пайплаина поиска
/// </summary>
/// <typeparam name="TContext">Тип обрабатываемого контекста</typeparam>
public interface IPipelineStep<TContext> where TContext : SearchContextBase
{
    void Execute(TContext context);
}

/// <summary>
/// Пайплаин поиска
/// </summary>
/// <typeparam name="TContext">Тип обрабатываемого контекста</typeparam>
public class SearchPipeline<TContext> : ISearchPipeline<TContext> where TContext : SearchContextBase
{
    private readonly List<IPipelineStep<TContext>> _steps = [];

    /// <summary>
    /// Добавляет инициализацию поисковых токенов
    /// </summary>
    /// <param name="normalizer">Нормалайзер для запроса</param>
    /// <param name="tokenizer">Токенайзер для запроса</param>
    /// <param name="wordsSearchSimilaritySettings">Настройки поиска схожих слов</param>
    /// <param name="alternativesFactory">Фабрика для альтернативных слов, доступен context.SplittedAndNormalizedQuery</param>
    /// <returns></returns>
    public SearchPipeline<TContext> AddSearchTokens(
        INormalizer? normalizer = null,
        ITokenizer? tokenizer = null,
        WordsSearchSimilaritySettings? wordsSearchSimilaritySettings = null,
        Func<TContext, AlternativesAndMultiplers>? alternativesFactory = null)
    {
        _steps.Add(new InitializeSearchTokensStep<TContext>(
            normalizer ?? DefaultNormalizer.Instance,
            tokenizer ?? DefaultTokenizer.Instance,
            wordsSearchSimilaritySettings ?? WordsSearchSimilaritySettings.Default,
            alternativesFactory));
        return this;
    }

    /// <summary>
    /// Добавляет поиск сущностей по индексу
    /// </summary>
    /// <param name="requestFactory">Фабрика запроса на поиск в индексе</param>
    /// <param name="scoreCalculator">Калькулятор скора для сущностей</param>
    public SearchPipeline<TContext> AddSearch(
        Func<TContext, IEnumerable<EntitiesSelectionOperation>> requestFactory,
        IScoreCalculator<TContext>? scoreCalculator = null)
    {
        _steps.Add(new SearchStep<TContext>(
            requestFactory,
            scoreCalculator ?? ScoreCalculatorBase<TContext>.Intance));
        return this;
    }

    public SearchPipeline<TContext> AddStep(IPipelineStep<TContext> step)
    {
        _steps.Add(step);
        return this;
    }

    public SearchPipeline<TContext> AddStep(Action<TContext> action)
    {
        _steps.Add(new DelegateStep<TContext>(action));
        return this;
    }

    public TContext Execute(TContext context)
    {
        foreach (var step in _steps)
        {
            step.Execute(context);
        }
        return context;
    }
}