using System.Runtime.InteropServices;
using QuerySeek.Models;
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

    public Dictionary<int, NgrammAssociation[]> GetWordsByNgramms()
    {
        Dictionary<int, List<NgrammAssociation>> wordsIdsByNgramms = [];

        foreach (KeyValuePair<string, int> item in Pairs.OrderBy(i => i.Key))
        {
            int[] ngramms = NgrammsWordsSearchHelper.GetNgramms(item.Key);

            for (byte i = 0; i < ngramms.Length; i++)
            {
                int ngramm = ngramms[i];
                ref List<NgrammAssociation>? ngrammAssociations = ref CollectionsMarshal.GetValueRefOrAddDefault(wordsIdsByNgramms, ngramm, out bool exists);

                if (!exists)
                    ngrammAssociations = [];

                ngrammAssociations!.Add(new(item.Value, i));
            }
        }

        return wordsIdsByNgramms.ToDictionary(i => i.Key, i => i.Value.ToArray());
    }
}
