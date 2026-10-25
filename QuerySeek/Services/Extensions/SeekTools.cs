using System.Collections.Frozen;
using System.Runtime;
using System.Runtime.InteropServices;
using MessagePack;
using QuerySeek.Interfaces;
using QuerySeek.Models;
using QuerySeek.Services.Building;
using QuerySeek.Services.Helpers;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Searching;

namespace QuerySeek.Services.Extensions;

public static class QS
{
    #region Tools
    public static Name Name<TNameType>(string name, TNameType nameType) where TNameType : Enum
        => new(name, Type(nameType));

    public static Name Name(string name, byte nameType)
        => new(name, nameType);

    public static Name Name(string name)
        => new(name, 0);

    public static byte Type<TType>(TType type) where TType : Enum
        => Convert.ToByte(type);

    public static byte[] Types<TType>(params TType[] types) where TType : Enum
        => Array.ConvertAll(types, type => Type(type));

    public static Key Key<TType>(TType type, int id) where TType : Enum
        => new(Type(type), id);

    public static Key Key(byte type, int id)
        => new(type, id);

    public static Key[] Keys<TType>(TType type, params int[] ids) where TType : Enum
        => Array.ConvertAll(ids, id => Key(type, id));

    public static Key[] Keys(byte type, params int[] ids)
        => Array.ConvertAll(ids, id => Key(type, id));

    public static bool IsWordSimilarityValid(Word queryWord, WordMatch wordMatch, double similarity)
        => (double)wordMatch.Score / queryWord.NGrammsHashes.Length >= similarity;

    public static bool IsWordSimilarityValid(this SearchContextBase context, WordMatch wordMatch, double similarity)
        => IsWordSimilarityValid(context.SearchWordsBundle[wordMatch.WordsBundlePosition].QueryWordAndAlternatives[0], wordMatch, similarity);
    #endregion

    #region Build
    public static IndexBuilder GetBuilder(INormalizer normalizer, ITokenizer nameTokenizer)
        => new(normalizer, nameTokenizer);

    public static IndexInstance Build(INormalizer normalizer, ITokenizer nameTokenizer, IEnumerable<IIndexedEntity> entities)
    {
        var builder = new IndexBuilder(normalizer, nameTokenizer);

        foreach (var entity in entities)
            builder.AddEntity(entity);

        return builder.Build();
    }

    public static async Task<IndexInstance> BuildAsync(INormalizer normalizer, ITokenizer nameTokenizer, IAsyncEnumerable<IIndexedEntity> entities)
    {
        var builder = new IndexBuilder(normalizer, nameTokenizer);

        await foreach (var entity in entities)
            builder.AddEntity(entity);

        return builder.Build();
    }

    #endregion

    #region Serialization

    /// <summary>
    /// Оптимизация и сжатие индекса после создания и десериализации
    /// </summary>
    /// <param name="index">Инстанс индекса</param>
    /// <param name="useFrozenCollections">Использовать ли Frozen коллекции внутри индекса. Не рекомендуется.</param>
    /// <param name="optimizeLinks">Оптимизировать ли массивы линков, продолжительная операция</param>
    /// <param name="gcCompactLOH">Сжать ли LOH и вызвать очистку мусора</param>
    public static void Inititalize(this IndexInstance index, bool useFrozenCollections = false, bool optimizeLinks = true, bool gcCompactLOH = true)
    {
        if (optimizeLinks)
        {
            LinksComparer linksComparer = new();

            foreach (IGrouping<byte, KeyValuePair<Key, EntityMeta>> item in index.Entities.GroupBy(i => i.Key.Type))
            {
                Dictionary<Key[], Key[]> uniqs = new(linksComparer);
                foreach (EntityMeta meta in item.Select(i => i.Value))
                {
                    if (meta.Links.Length == 0) continue;

                    ref var uniqLinks = ref CollectionsMarshal.GetValueRefOrAddDefault(uniqs, meta.Links, out var exists);

                    if (!exists) uniqLinks = meta.Links;
                    else meta.Links = uniqLinks!;
                }
            }
        }

        if (useFrozenCollections)
        {
            //По замерам получилось только большее потребление памяти 20% и просадка производительности в 2 раза ¯\_(ツ)_/¯
            //Возможно словари внутри поисковой мапы слишком маленькие и выбранная стратегия медленне, но производительность падает и на коллекциях сущностей и слов

            //Оптимизация словарей
            index.Entities = index.Entities.ToFrozenDictionary();
            index.WordsIdsByNgramms = index.WordsIdsByNgramms.ToFrozenDictionary();
            //Оптимизация поисковой мапы
            for (int i = 0; i < index.EntitiesSearchMap.Length; i++)
            {
                KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[] matchesByTypes = index.EntitiesSearchMap[i];

                for (int j = 0; j < matchesByTypes.Length; j++)
                {
                    KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>> current = matchesByTypes[j];
                    matchesByTypes[j] = new(current.Key, current.Value.ToFrozenDictionary());
                }
            }
        }

        if (gcCompactLOH)
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        }
    }

    public static void WriteIndex(IndexInstance index, string filePath)
        => WriteObject(filePath, index);

    public static IndexInstance ReadIndex(string filePath, bool gcCompactLOH = true)
    {
        IndexInstance index = ReadAndDeserializeObject<IndexInstance>(filePath);
        index.Inititalize(false, gcCompactLOH: gcCompactLOH);

        return index;
    }

    public static T ReadAndDeserializeObject<T>(string filePath) where T : class
    {
        using Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return MessagePackSerializer.Deserialize<T>(stream);
    }

    public static void WriteObject(string filePath, object obj)
    {
        string? directoryPath = Path.GetDirectoryName(filePath);

        if (directoryPath is null)
            return;

        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        using Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

        MessagePackSerializer.Serialize(stream, obj);
    }
    #endregion
}
