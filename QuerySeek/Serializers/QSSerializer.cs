using MessagePack;
using QuerySeek.Models;
using QuerySeek.Helpers;

namespace QuerySeek.Serializers;

public static class QSSerializer
{
    private static readonly string EntitiesFileName = "entities.dat";
    private static readonly string WordsIndexFileName = "words_index.dat";
    private static readonly string SearchMapFileName = "search_map.dat";

    private static readonly MessagePackSerializerOptions? SerializerOptions
        = null;
        //= MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    /// <summary>
    /// Записывает файлы индекса в директорию
    /// </summary>
    /// <param name="directoryPath">Путь до директории</param>
    /// <param name="index">Индекс</param>
    public static async Task WriteIndexAsync(string directoryPath, IndexInstance index)
    {
        Directory.CreateDirectory(directoryPath);

        string entitiesPath = Path.Combine(directoryPath, EntitiesFileName);
        string wordsPath = Path.Combine(directoryPath, WordsIndexFileName);
        string searchMapPath = Path.Combine(directoryPath, SearchMapFileName);

        await WriteMPObjectAsync(entitiesPath, index.Entities);
        await WriteMPObjectAsync(wordsPath, index.WordsIdsByNgramms);
        await WriteMPObjectAsync(searchMapPath, index.EntitiesSearchMap);
    }

    /// <summary>
    /// Читает индекс из директории
    /// </summary>
    /// <param name="directoryPath">Путь до директории</param>
    public static async Task<IndexInstance?> ReadIndexAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return null;

        string entitiesPath = Path.Combine(directoryPath, EntitiesFileName);
        string wordsPath = Path.Combine(directoryPath, WordsIndexFileName);
        string searchMapPath = Path.Combine(directoryPath, SearchMapFileName);

        var entities = await ReadMPObjectAsync<IReadOnlyDictionary<Key, EntityMeta>>(entitiesPath);
        DeduplicateLinks(entities);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

        var words = await ReadMPObjectAsync<IReadOnlyDictionary<int, NgrammAssociation[]>>(wordsPath);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

        var searchMap = await ReadMPObjectAsync<KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[][]>(searchMapPath);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

        IndexInstance indexInstance = new()
        {
            Entities = entities,
            WordsIdsByNgramms = words,
            EntitiesSearchMap = searchMap
        };

        return indexInstance;
    }

    private static void DeduplicateLinks(IReadOnlyDictionary<Key, EntityMeta> entities)
    {
        LinksComparer linksComparer = new();

        foreach (IGrouping<byte, KeyValuePair<Key, EntityMeta>> item in entities.GroupBy(i => i.Key.Type))
        {
            HashSet<Key[]> uniqs = new(linksComparer);
            foreach (EntityMeta meta in item.Select(i => i.Value))
            {
                if (meta.Links.Length == 0) continue;

                if (uniqs.TryGetValue(meta.Links, out Key[]? actualValue))
                    meta.Links = actualValue;
                else
                    uniqs.Add(meta.Links);
            }
        }
    }

    public static async Task<T> ReadMPObjectAsync<T>(string filePath) where T : class
    {
        using Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await MessagePackSerializer.DeserializeAsync<T>(stream, SerializerOptions).ConfigureAwait(false);
    }

    public static async Task WriteMPObjectAsync(string filePath, object obj)
    {
        string? directoryPath = Path.GetDirectoryName(filePath);

        if (directoryPath is null)
            return;

        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        using Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await MessagePackSerializer.SerializeAsync(stream, obj, SerializerOptions).ConfigureAwait(false);
    }
}
