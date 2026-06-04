using MessagePack;
using QuerySeek.Interfaces;
using QuerySeek.Models;
using QuerySeek.Services.Building;
using QuerySeek.Services.Normalizing;
using QuerySeek.Services.Splitting;

namespace QuerySeek.Services.Extensions;

public static class QS
{
    #region Tools
    public static Phrase Phrase<TPhraseType>(string phrase, TPhraseType phraseType) where TPhraseType : Enum
        => new(phrase, Type(phraseType));

    public static Phrase Phrase(string phrase, byte phraseType)
        => new(phrase, phraseType);

    public static Phrase Phrase(string phrase)
        => new(phrase, 0);

    public static byte Type<TType>(TType type)
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
    #endregion

    #region Build
    public static IndexBuilder GetBuilder(INormalizer normalizer, IPhraseSplitter phraseSplitter)
        => new(normalizer, phraseSplitter);

    public static IndexInstance Build(INormalizer normalizer, IPhraseSplitter phraseSplitter, IEnumerable<IIndexedEntity> entities)
    {
        var builder = new IndexBuilder(normalizer, phraseSplitter);

        foreach (var entity in entities)
            builder.AddEntity(entity);

        return builder.Build();
    }

    public static async Task<IndexInstance> Build(INormalizer normalizer, IPhraseSplitter phraseSplitter, IAsyncEnumerable<IIndexedEntity> entities)
    {
        var builder = new IndexBuilder(normalizer, phraseSplitter);

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

public record struct Phrase(string Text, byte PhraseType);
