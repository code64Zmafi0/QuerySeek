using MessagePack;
using QuerySeek.Interfaces;
using QuerySeek.Models;
using QuerySeek.Services.Building;
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

    public static bool WordIsFullMatched(Word queryWord, WordCompareResult wordCompareResult)
        => queryWord.NGrammsHashes.Length == wordCompareResult.MatchLength;

    public static bool WordIsFullMatched(SearchContextBase context, WordCompareResult wordCompareResult)
        => WordIsFullMatched(context.NgrammedQuery[wordCompareResult.QueryWordPosition].QueryWord, wordCompareResult);
    #endregion

    #region Build
    public static IndexBuilder GetBuilder(INormalizer normalizer, INameTokenizer nameTokenizer)
        => new(normalizer, nameTokenizer);

    public static IndexInstance Build(INormalizer normalizer, INameTokenizer nameTokenizer, IEnumerable<IIndexedEntity> entities)
    {
        var builder = new IndexBuilder(normalizer, nameTokenizer);

        foreach (var entity in entities)
            builder.AddEntity(entity);

        return builder.Build();
    }

    public static async Task<IndexInstance> BuildAsync(INormalizer normalizer, INameTokenizer nameTokenizer, IAsyncEnumerable<IIndexedEntity> entities)
    {
        var builder = new IndexBuilder(normalizer, nameTokenizer);

        await foreach (var entity in entities)
            builder.AddEntity(entity);

        return builder.Build();
    }

    #endregion

    #region Serialization
    public static void WriteIndex(IndexInstance index, string filePath)
        => WriteObject(filePath, index);

    public static IndexInstance ReadIndex(string filePath, bool gcCompactLOH = true)
    {
        IndexInstance index = ReadAndDeserializeObject<IndexInstance>(filePath);
        index.Trim(gcCompactLOH);

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
