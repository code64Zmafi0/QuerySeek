namespace QuerySeek.Services.Searching.Scoring;

/// <summary>
/// Определяет алгоритм калькулирования Score для сущностей
/// </summary>
/// <typeparam name="TContext">Тип поискового контекста</typeparam>
public interface IScoreCalculator<TContext> where TContext : SearchContextBase
{
    /// <summary>
    /// Метод калькулирования. Реализация должна задавать EntitySearchResult.Score
    /// </summary>
    /// <param name="context">Поисковой контекст</param>
    /// <param name="entitySearchResult">Сущность для калькуляции</param>
    void CalculateTextScore(TContext context, EntitySearchResult entitySearchResult);
}
