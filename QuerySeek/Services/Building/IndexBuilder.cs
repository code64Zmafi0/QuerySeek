using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuerySeek.Interfaces;
using QuerySeek.Models;
using QuerySeek.Services.Extensions;
using QuerySeek.Services.Helpers;
using QuerySeek.Services.Normalizing;

namespace QuerySeek.Services.Building;

public class IndexBuilder(INormalizer normalizer, INameTokenizer nameTokenizer)
{
    private readonly Dictionary<Key, EntityMeta> Entities = [];
    private readonly Dictionary<Key, HashSet<Key>> Childs = [];
    private readonly EntitiesByWordsSearchMapBuilder EntitiesByWordsSearchMapBuilder = new();
    private readonly WordsIndexBuilder WordsBundle = new();
    private readonly StringArraySequenceComparer NamesComparer = new();

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

        IEnumerable<Name> names = indexedEntity.GetNames();

        foreach ((string[] tokenizedName, byte nameType) in GetNamesToBuild(names, normalizer, nameTokenizer))
        {
            for (byte wordNamePosition = 0; wordNamePosition < tokenizedName.Length && wordNamePosition < byte.MaxValue; wordNamePosition++)
            {
                string word = tokenizedName[wordNamePosition];
                int wordId = WordsBundle.GetWordId(word);

                WordMatchMeta wordMatchMeta = new(key.Id, wordNamePosition, nameType);
                EntitiesByWordsSearchMapBuilder.AddMatch(wordId, key.Type, containerKey, wordMatchMeta);
            }
        }

        Entities.Add(key, new([.. linksKeys]));
    }

    private IEnumerable<(string[] TokenizedName, byte NameType)> GetNamesToBuild(
        IEnumerable<Name> names,
        INormalizer normalizer,
        INameTokenizer nameTokenizer)
        => [.. names
            .Select(name =>
            {
                string[] tokenizedName = TextPreprocessor.PreprocessName(nameTokenizer, normalizer, name.Text);
                return (tokenizedName, name.NameType);
            })
            //Если после нормализации получились одинаковые - убираем дубликаты
            .DistinctBy(i => i.tokenizedName, NamesComparer)];

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

        return new IndexInstance()
        {
            Entities = Entities,
            EntitiesSearchMap = EntitiesByWordsSearchMapBuilder.CreateMap(),
            WordsIdsByNgramms = WordsBundle.GetWordsByNgramms(),
        };
    }
}
