using QuerySeek.Models;

namespace QuerySeek.Services.Searching;

public class TypeSearchResult(byte type, EntityMatchesBundle[] result)
{
    public byte Type { get; } = type;

    public EntityMatchesBundle[] Result { get; } = result;
}

public class EntityMatchesBundle(Key key, EntityMeta entityMeta)
{
    public EntityMeta EntityMeta { get; } = entityMeta;

    public List<WordCompareResult> WordsMatches { get; } = new(1);

    public List<AdditionalRule> Rules { get; } = [];

    public Key Key => key;

    public int RulesScore => Rules.Sum(i => i.Score);

    public int Prescore;

    public int Score;

    internal void AddMatch(in WordCompareResult wordCompareResult)
    {
        WordsMatches.Add(wordCompareResult);
        Prescore += wordCompareResult.MatchLength;
    }
}

public readonly record struct WordCompareResult(
    byte NameWordPosition,
    byte PhraseType,
    byte QueryWordPosition,
    byte MatchLength);


public readonly record struct IndexWordSearchInfo(
    byte Mathes,
    byte Misses,
    byte PreviousMatch)
{
    public byte Score => (byte)(Mathes > Misses ? Mathes - (Misses * 0.5): 0);
}

public record AdditionalRule(string Name, int Score, double multipler = 1);
