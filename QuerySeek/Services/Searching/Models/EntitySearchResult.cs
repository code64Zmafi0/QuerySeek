using QuerySeek.Models;

namespace QuerySeek.Services.Searching.Models;

public class TypeSearchResult(byte type, EntitySearchResult[] result)
{
    public readonly byte Type = type;

    public readonly EntitySearchResult[] Result = result;
}

public class EntitySearchResult(Key key, EntityMeta meta)
{
    public readonly Key Key = key;

    public readonly EntityMeta Meta = meta;

    public readonly List<WordCompareResult> WordsMatches = new(1);

    public readonly List<AdditionalRule> Rules = [];

    public int Prescore;

    public int Score;

    public Key[] GetLinks()
        => Meta.Links;

    public Key[] GetChilds()
        => Meta.Childs;

    public void AddRule(AdditionalRule rule)
        => Rules.Add(rule);

    internal void AddMatch(WordCompareResult wordCompareResult)
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

public record AdditionalRule(string Name, int Score = 0, double Multipler = 1);
