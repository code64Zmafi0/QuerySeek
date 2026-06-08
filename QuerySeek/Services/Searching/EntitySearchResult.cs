using QuerySeek.Models;

namespace QuerySeek.Services.Searching;

public class TypeSearchResult(byte type, EntitySearchResult[] result)
{
    public byte Type { get; } = type;

    public EntitySearchResult[] Result { get; } = result;
}

public class EntitySearchResult(Key key, EntityMeta meta)
{
    public Key Key => key;

    public EntityMeta Meta => meta;

    public List<WordCompareResult> WordsMatches { get; } = new(1);

    public List<AdditionalRule> Rules { get; } = [];

    public int Prescore;

    public int Score;

    public Key[] GetLinks()
        => Meta.Links;

    public Key[] GetChilds()
        => Meta.Childs;

    public void AddRule(AdditionalRule rule)
        => Rules.Add(rule);

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

public readonly record struct WordNgrammSearchState(
    byte Matches,
    byte Misses,
    byte PreviousMatch)
{
    public byte Score => (byte)(Matches > Misses ? Matches - (Misses * 0.5) : 0);
}

public record AdditionalRule(string Name, int Score = 0, double Multipler = 1);
