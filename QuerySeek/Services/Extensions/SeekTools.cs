using System.Collections.Frozen;
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

    public static bool IsWordSimilarityValid(Word queryWord, WordMatch wordMatch, double similarity)
        => (double)wordMatch.Score / queryWord.NGrammsHashes.Length >= similarity;

    public static bool IsWordSimilarityValid(this QueryWordContainer[] searchWordsBundle, WordMatch wordMatch, double similarity)
        => IsWordSimilarityValid(searchWordsBundle[wordMatch.WordsBundlePosition].Word, wordMatch, similarity);
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

    /// <summary>
    /// Привести коллекции к Frozen
    /// </summary>
    public static void UseFrozenCollections(this IndexInstance index)
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

    #endregion
}
