namespace QuerySeek.Services.Helpers;

/// <summary>
/// Ограничитель выборки по схожим словам
/// </summary>
public struct ByWordSearchStopManager
{
    public ByWordSearchStopManager(int quantity)
    {
        _quantity = quantity;
        _matchesCount = 0;
    }

    public ByWordSearchStopManager()
    {
        _quantity = 45;
        _matchesCount = 0;
    }

    private int _matchesCount;

    private int _quantity;

    public void IncrementMatch()
        => _matchesCount++;

    public readonly bool NeedContinue
        => _matchesCount < _quantity;
}
