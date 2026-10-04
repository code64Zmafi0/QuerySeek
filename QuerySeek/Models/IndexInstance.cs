using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime;
using MessagePack;

namespace QuerySeek.Models;

[MessagePackObject]
public class IndexInstance
{
    public static readonly IndexInstance Empty = new();

    /// <summary>
    /// Информация для сущностей о линках и потомках
    /// </summary>
    [Key(1)]
    public IReadOnlyDictionary<Key, EntityMeta> Entities { get; internal set; } = ImmutableDictionary<Key, EntityMeta>.Empty;

    /// <summary>
    /// ID слов по хешу нграмма
    /// </summary>
    [Key(2)]
    public IReadOnlyDictionary<int, NgrammAssociation[]> WordsIdsByNgramms { get; internal set; } = ImmutableDictionary<int, NgrammAssociation[]>.Empty;

    /// <summary>
    /// Словарь WordId -> Types -> Containers -> MatchesToEntites
    /// Так как ид слов последовательны использован массив вместо словаря - так как словарь большого размера в разы медленней на обращени и занимает больше места
    /// </summary>
    [Key(3)]
    public KeyValuePair<byte /*TypeId*/, IReadOnlyDictionary</*ContainerKey*/ Key, WordMatchMeta[]>>[][/*WordId*/] EntitiesSearchMap { get; set; } = [];

    [IgnoreMember]
    public int EntitesCount => Entities.Count;

    public int GetEntitesCount(byte type) => Entities.Keys.Count(i => i.Type == type);

    /// <summary>
    /// Оптимизация и сжатие индекса после создания и десериализации
    /// </summary>
    /// <param name="useFrozenCollections">Использовать ли Frozen коллекции внутри индекса. Не рекомендуется.</param>
    /// <param name="gcCompactLOH">Сжать ли LOH и вызвать очистку мусора</param>
    public void Inititalize(bool useFrozenCollections = false, bool gcCompactLOH = true)
    {
        //Подменяем пустые массивы одной ссылкой
        foreach (EntityMeta meta in Entities.Values)
        {
            if (meta.Childs.Length == 0)
                meta.Childs = Array.Empty<Key>();

            if (meta.Links.Length == 0)
                meta.Links = Array.Empty<Key>();

        }

        if (useFrozenCollections)
        {
            //Оптимизация словарей
            Entities = Entities.ToFrozenDictionary();
            WordsIdsByNgramms = WordsIdsByNgramms.ToFrozenDictionary();
            //Оптимизация поисковой мапы
            for (int i = 0; i < EntitiesSearchMap.Length; i++)
            {
                KeyValuePair<byte, IReadOnlyDictionary<Key, WordMatchMeta[]>>[] matchesByTypes = EntitiesSearchMap[i];

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
}
