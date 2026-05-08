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

    public static byte Type<TType>(TType type)
        => Convert.ToByte(type);

    public static byte[] Keys<TType>(params TType[] types) where TType : Enum
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

    #region NgrammsLogic
    public const short NGRAM_LENGTH = 3;

    public static int[] GetNgramms(string word)
    {
        const char SpaceChar = ' ';

        int spaceLength = NGRAM_LENGTH - 1;
        int totalLength =  word.Length + spaceLength * 2;

        //Буффер нормализации
        Span<char> buffer = totalLength <= 256
            ? stackalloc char[totalLength]
            : new char[totalLength];

        //Заполняем спейсы
        buffer[..spaceLength].Fill(SpaceChar);
        buffer[^spaceLength..].Fill(SpaceChar);

        //Заполняем слово
        word.CopyTo(buffer[spaceLength..]);

        //Просчитываем результат слова
        int[] result = new int[word.Length + NGRAM_LENGTH - 1];

        for (int i = 0; i <= buffer.Length - NGRAM_LENGTH; i++)
        {
            //Вычисялем хеш нграмма
            Span<char> nGramm = buffer.Slice(i, NGRAM_LENGTH);

            int num = 5381;
            int num2 = num;
            for (int k = 0; k < nGramm.Length; k += 2)
            {
                num = (num << 5) + num ^ nGramm[k];

                if (k + 1 < nGramm.Length)
                    num2 = (num2 << 5) + num2 ^ nGramm[k + 1];
            }
            int hash = num + num2 * 1566083941;

            result[i] = hash;
        }

        return result;
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
