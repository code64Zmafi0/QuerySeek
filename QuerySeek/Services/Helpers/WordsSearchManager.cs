namespace QuerySeek.Services.Helpers;

/// <summary>
/// Счетчик совпавших схожих слов для 1 слова из запроса, чтобы остановить поиск по схожим словам если найдено определенное количество слов
/// </summary>
/// <param name="quantity"></param>
public class WordsSearchManager(int quantity)
{
    private int MatchesCount;

    public void IncrementMatch()
        => MatchesCount++;

    public bool NeedContinue
        => MatchesCount <= quantity;
}
