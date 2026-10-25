namespace QuerySeek.Services.Helpers;

/// <summary>
/// Ограничитель выборки по схожим словам
/// </summary>
/// <param name="quantity">Количество совпадений для остановки просчета похожих</param>
public struct WordsSearchStopManager(int quantity)
{
    private int MatchesCount;

    public void IncrementMatch()
        => MatchesCount++;

    public readonly bool NeedContinue
        => MatchesCount < quantity;
}
