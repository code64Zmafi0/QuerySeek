using System.Runtime.InteropServices;
using QuerySeek.Models;

namespace QuerySeek.Services.Building;

public class EntitiesByWordsSearchMapBuilder()
{
    public Dictionary<int /*WordId*/, Dictionary<byte /*TypeId*/, Dictionary</*ByNodeKey*/ Key, List<WordMatchMeta>>>> EntitiesByWords { get; } = [];

    public void AddMatch(int wordId, byte entityType, Key containerKey, WordMatchMeta wordMatch)
    {
        ref var wordMatches = ref CollectionsMarshal.GetValueRefOrAddDefault(EntitiesByWords, wordId, out var exists);

        if (!exists)
            wordMatches = [];

        ref var matchesBundle = ref CollectionsMarshal.GetValueRefOrAddDefault(wordMatches!, entityType, out exists);

        if (!exists)
            matchesBundle = [];

        ref var matches = ref CollectionsMarshal.GetValueRefOrAddDefault(matchesBundle!, containerKey, out exists);

        if (!exists)
            matches = [];

        matches!.Add(wordMatch);
    }

    public KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[][] CreateMap()
    {
        var entitiesByWords = new KeyValuePair<byte /*TypeId*/, Dictionary</*ContainerKey*/ Key, WordMatchMeta[]>>[EntitiesByWords.Count][];

        foreach (var wordMatch in EntitiesByWords)
        {
            entitiesByWords[wordMatch.Key] = wordMatch.Value
                .OrderBy(x => x.Key)
                .Select(x => new KeyValuePair<byte, Dictionary</*ContainerKey*/ Key, WordMatchMeta[]>>(
                    x.Key, 
                    x.Value.ToDictionary(
                        i => i.Key,
                        i => i.Value.ToArray())))
                .ToArray();
        }

        return entitiesByWords;
    }
}
