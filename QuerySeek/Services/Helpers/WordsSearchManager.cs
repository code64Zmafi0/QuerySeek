namespace QuerySeek.Services.Helpers;

public class WordsSearchManager(int quantity)
{
    private int MatchesCount;

    public void IncrementMatch()
        => MatchesCount++;

    public bool NeedContinue
        => MatchesCount <= quantity;
}
