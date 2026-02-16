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
        => new(phrase, Convert.ToByte(phraseType));

    public static Phrase Phrase(string phrase, byte phraseType)
        => new(phrase, phraseType);

    public static Phrase Phrase(string phrase)
        => new(phrase, 0);

    public static Key Key<TType>(TType type, int id) where TType : Enum
        => new(Convert.ToByte(type), id);

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

    #region NgrammsLogic
    public const short NGRAM_LENGTH = 3;

    public static int[] GetNgrams(string word)
    {
        var space = string.Concat(Enumerable.Repeat(' ', NGRAM_LENGTH - 1));

        var normalized = $"{space}{word}{space}";

        int[] result = new int[word.Length + NGRAM_LENGTH - 1];

        for (var i = 0; i <= normalized.Length - NGRAM_LENGTH; i++)
        {
            var nGramm = normalized.AsSpan(i, NGRAM_LENGTH);
            result[i] = GetNGrammHash(in nGramm);
        }

        return result;
    }

    private static int GetNGrammHash(in ReadOnlySpan<char> value)
    {
        int num = 5381;
        int num2 = num;
        for (int i = 0; i < value.Length; i += 2)
        {
            num = (num << 5) + num ^ value[i];

            if (i + 1 < value.Length)
                num2 = (num2 << 5) + num2 ^ value[i + 1];
        }
        return num + num2 * 1566083941;
    }
    #endregion

    #region Serialization
    public static void WriteIndex(IndexInstance index, string filePath)
        => WriteObject(filePath, index);

    public static IndexInstance ReadIndex(string filePath)
    {
        IndexInstance index = ReadAndDeserializeObject<IndexInstance>(filePath);
        index.Trim();

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
