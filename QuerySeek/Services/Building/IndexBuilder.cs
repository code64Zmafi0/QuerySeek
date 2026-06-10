using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuerySeek.Interfaces;
using QuerySeek.Models;
using QuerySeek.Services.Extensions;
using QuerySeek.Services.Normalizing;

namespace QuerySeek.Services.Building;

public class IndexBuilder(INormalizer normalizer, IPhraseSplitter phraseSplitter)
{
    private readonly Dictionary<Key, EntityMeta> Entities = [];
    private readonly Dictionary<Key, HashSet<Key>> Childs = [];
    private readonly EntitiesByWordsBuilder EntitiesByWordsIndex = new();
    private readonly WordsIndexBuilder WordsBundle = new();

    public void AddEntity(in IIndexedEntity indexedEntity)
    {
        Key key = indexedEntity.GetKey();
        Key containerKey = indexedEntity.GetContainer() ?? Key.Default;

        if (Entities.ContainsKey(key))
            return;

        HashSet<Key> linksKeys = [.. indexedEntity.GetLinks()];

        foreach (Key parent in linksKeys)
        {
            ref HashSet<Key>? set = ref CollectionsMarshal.GetValueRefOrAddDefault(Childs, parent, out var exists);

            if (!exists)
                set = [];

            set!.Add(key);
        }

        IEnumerable<Phrase> names = indexedEntity.GetNames();

        (string[] TokenizedPhrase, byte PhraseType)[] namesToBuild = GetNamesToBuild(names, normalizer, phraseSplitter);
        for (int nameIndex = 0; nameIndex < namesToBuild.Length; nameIndex++)
        {
            (string[] phrase, byte phraseType) = namesToBuild[nameIndex];

            for (byte wordNamePosition = 0; wordNamePosition < phrase.Length && wordNamePosition < byte.MaxValue; wordNamePosition++)
            {
                string word = phrase[wordNamePosition];
                int wordId = WordsBundle.GetWordId(word);

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
            string[] tokenizedPhrase = TextPreprocessor.PreprocessPhrase(phraseSplitter, normalizer, phrase.Text);
            return (tokenizedPhrase, phrase.PhraseType);
        })];

    public IndexInstance Build()
    {
        //Очистка возможной неконсистентности данных
        foreach (Key entityKey in Entities.Keys)
        {
            ref EntityMeta meta = ref CollectionsMarshal.GetValueRefOrNullRef(Entities, entityKey);

            if (!Unsafe.IsNullRef(ref meta))
            {
                meta.Links = [.. meta.Links.Where(Entities.ContainsKey)];
            }
        }

        foreach (KeyValuePair<Key, HashSet<Key>> item in Childs)
        {
            ref EntityMeta meta = ref CollectionsMarshal.GetValueRefOrNullRef(Entities, item.Key);

            if (!Unsafe.IsNullRef(ref meta))
            {
                meta.Childs = [.. item.Value.Where(Entities.ContainsKey)];
            }
        }
        ;

        return new IndexInstance()
        {
            Entities = Entities,
            EntitiesByWordsIndex = EntitiesByWordsIndex.CreateIndex(),
            WordsIdsByNgramms = WordsBundle.GetWordsByNgramms(),
        };
    }
}
