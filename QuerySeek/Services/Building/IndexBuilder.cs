using System.Runtime.InteropServices;
using QuerySeek.Interfaces;
using QuerySeek.Models;
using QuerySeek.Services.Extensions;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Splitting;

namespace QuerySeek.Services.Building;

public class IndexBuilder(INormalizer normalizer, IPhraseSplitter phraseSplitter)
{
    private readonly Dictionary<Key, EntityMeta> Entities = [];
    private readonly Dictionary<Key, HashSet<Key>> Childs = [];
    private readonly EntitiesByWordsBuilder EntitiesByWordsIndex = new();
    private readonly WordsBuildBundle WordsBundle = new();

    public void AddEntity(in IIndexedEntity indexedEntity)
    {
        Key key = indexedEntity.GetKey();
        Key? containerKey = indexedEntity.GetContainer();

        if (Entities.ContainsKey(key))
            return;

        HashSet<Key> linksKeys = [.. indexedEntity.GetLinks()];

        foreach (Key parent in indexedEntity.GetParents())
        {
            ref var set = ref CollectionsMarshal.GetValueRefOrAddDefault(Childs, parent, out var exists);

            if (!exists)
                set = [];

            set!.Add(key);
        }

        IEnumerable<Phrase> names = indexedEntity.GetNames();
        HashSet<int> uniqWords = [];
        (string[] TokenizedPhrase, byte PhraseType)[] namesToBuild = GetNamesToBuild(names, normalizer, phraseSplitter);
        for (int nameIndex = 0; nameIndex < namesToBuild.Length; nameIndex++)
        {
            (string[] phrase, byte phraseType) = namesToBuild[nameIndex];

            for (byte wordNamePosition = 0; wordNamePosition < phrase.Length && wordNamePosition < byte.MaxValue; wordNamePosition++)
            {
                string word = phrase[wordNamePosition];
                var wordId = WordsBundle.GetWordId(word);

                if (!uniqWords.Add(wordId))
                    continue;

                WordMatchMeta wordMatchMeta = new(key.Id, wordNamePosition, phraseType);
                EntitiesByWordsIndex.AddMatch(wordId, key.Type, containerKey, wordMatchMeta);
            }
        }

        Entities.Add(key, new([.. linksKeys]));
    }

    private static (string[] TokenizedPhrase, byte PhraseType)[] GetNamesToBuild(
        IEnumerable<Phrase> phrases,
        INormalizer normalizer,
        IPhraseSplitter phraseSplitter)
        => [.. phrases.Select(phrase =>
        {
            string normalizedPhrase = normalizer.Normalize(phrase.Text!);
            string[] tokenizedPhrase = phraseSplitter.Tokenize(normalizedPhrase);
            return (tokenizedPhrase, phrase.PhraseType);
        })];

    public IndexInstance Build()
    {
        bool CheckMeta(Key key, out EntityMeta? meta)
        {
            meta = null;

            return Entities.TryGetValue(key, out meta);
        }

        foreach (var entity in Entities.Values)
        {
            entity.Childs = [.. entity.Childs.Where(i => CheckMeta(i, out _))];
        }

        foreach (KeyValuePair<Key, HashSet<Key>> item in Childs)
        {
            if (CheckMeta(item.Key, out var meta))
            {
                meta!.Childs = [.. item.Value.Where(i => CheckMeta(i, out _))];
            }
        };

        Dictionary<int, HashSet<int>> wordsIdsByNgramms = [];
        int[] wordsByIds = new int[WordsBundle.Pairs.Count];

        foreach (var item in WordsBundle.GetWordsByIds())
        {
            int[] ngramms = SeekTools.GetNgrams(item.Key);
            wordsByIds[item.Value] = ngramms.Length;

            for (int i = 0; i < ngramms.Length; i++)
            {
                int ngramm = ngramms[i];
                ref var words = ref CollectionsMarshal.GetValueRefOrAddDefault(wordsIdsByNgramms, ngramm, out var exists);

                if (!exists)
                    words = [];

                words!.Add(item.Value);
            }
        }

        return new IndexInstance()
        {
            Entities = Entities,
            EntitiesByWordsIndex = EntitiesByWordsIndex.CreateIndex(),
            WordsIdsByNgramms = wordsIdsByNgramms.ToDictionary(i => i.Key, i => i.Value.ToArray()),
        };
    }
}

public class WordsBuildBundle()
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

    public IEnumerable<KeyValuePair<string, int>> GetWordsByIds()
    {
        foreach (var wordIdPair in Pairs.OrderBy(i => i.Key))
            yield return wordIdPair;
    }
}
