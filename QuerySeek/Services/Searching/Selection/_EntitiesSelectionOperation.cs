namespace QuerySeek.Services.Searching.Requests;

/// <summary>
/// Базовое определние для запроса на выборку в индексе. Шаг SELECTING
/// </summary>
/// <param name="targetType">Текущий тип отбираемых сущностей</param>
public abstract class EntitiesSelectionOperation(byte targetType)
{
    public byte TargetType { get; } = targetType;

    /// <summary>
    /// Выполняет процесс поиска сущностей в индексе и заполняет результат в SearchContext
    /// </summary>
    /// <param name="searchContext"></param>
    /// <param name="ct"></param>
    public abstract void Process(SearchContextBase searchContext, CancellationToken ct);
}
