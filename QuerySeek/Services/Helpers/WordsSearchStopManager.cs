namespace QuerySeek.Services.Helpers;

/// <summary>
/// Ограничитель 
/// </summary>
/// <param name="quantity"></param>
public struct WordsSearchStopManager(int quantity)
{
    private int MatchesCount;

    public void IncrementMatch()
        => MatchesCount++;

    public readonly bool NeedContinue
        => MatchesCount < quantity;
}
