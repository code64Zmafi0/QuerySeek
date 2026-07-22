using System.Runtime.InteropServices;
using QuerySeek.Services.Helpers;

namespace QuerySeek.Services.Building;

public class WordsIndexBuilder()
{
    private int CurrentId = 0;

    public readonly Dictionary<string, int> Pairs = [];

    public int GetWordId(string word)
    {
        ref var id = ref CollectionsMarshal.GetValueRefOrAddDefault(Pairs, word, out var exists);
        if (exists)
            return id;

        id = CurrentId++;
        return id;
    }

    public Dictionary<int, int[]> GetWordsByNgramms()
    {
        Dictionary<int, HashSet<int>> wordsIdsByNgramms = [];

        foreach (KeyValuePair<string, int> item in Pairs.OrderBy(i => i.Key))
        {
            int[] ngramms = NgrammsWordsSearchHelper.GetNgramms(item.Key);

            for (int i = 0; i < ngramms.Length; i++)
            {
                int ngramm = ngramms[i];
                ref HashSet<int>? words = ref CollectionsMarshal.GetValueRefOrAddDefault(wordsIdsByNgramms, ngramm, out var exists);

                if (!exists)
                    words = [];

                words!.Add(item.Value);
            }
        }

        return wordsIdsByNgramms.ToDictionary(i => i.Key, i => i.Value.ToArray());
    }
}
