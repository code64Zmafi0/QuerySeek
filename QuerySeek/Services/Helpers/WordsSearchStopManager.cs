namespace QuerySeek.Services.Helpers;

/// <summary>
/// MUTABLE STRUCT! Счетчик совпавших схожих слов для 1 слова из запроса, чтобы остановить поиск по схожим словам если найдено определенное количество слов
/// </summary>
/// <param name="quantity"></param>
/// <param name="mathesCount"></param>
public struct WordsSearchStopManager(int quantity)
{
    private int MatchesCount;

    public void IncrementMatch()
        => MatchesCount++;

    public readonly bool NeedContinue
        => MatchesCount < quantity;
}
