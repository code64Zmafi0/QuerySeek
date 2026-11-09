namespace QuerySeek.Services.Helpers;

/// <summary>
/// Ограничитель выборки по схожим словам
/// </summary>
/// <param name="quantity">Количество совпадений для остановки просчета похожих</param>
public struct ByWordSearchStopManager(int quantity = 45)
{
    private int MatchesCount;

    public void IncrementMatch()
        => MatchesCount++;

    public readonly bool NeedContinue
        => MatchesCount < quantity;
}
