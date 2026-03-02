namespace QuerySeek.Services.Searching;

public class WordsSearchManager(int quantity)
{
    private int MatchesCount;

    public void IncrementMatch()
        => MatchesCount++;

    public bool NeedContinue
        => MatchesCount <= quantity;
}
