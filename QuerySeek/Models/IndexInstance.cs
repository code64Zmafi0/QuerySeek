using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MessagePack;

namespace QuerySeek.Models;

[MessagePackObject]
public class IndexInstance
{
    public static readonly IndexInstance Empty = new();

    public IndexInstance() { }

    [Key(1)]
    public Dictionary<Key, EntityMeta> Entities { get; set; } = [];

    [Key(2)]
    public Dictionary<int, int[]> WordsIdsByNgramms { get; set; } = [];

    [Key(3)]
    public EntitiesByWordsIndex EntitiesByWordsIndex { get; set; } = new();

    [IgnoreMember]
    public int EntitesCount => Entities.Count;

    public int GetEntitesCount(byte type) => Entities.Keys.Count(i => i.Type == type);

    /// <summary>
    /// Сжатие данных после десериализации
    /// </summary>
    public void Trim(bool gcCompactLOH = true)
    {
        //Подменяем пустые массивы одной ссылкой
        foreach (Key key in Entities.Keys)
        {
            ref EntityMeta meta = ref CollectionsMarshal.GetValueRefOrNullRef(Entities, key);

            if (!Unsafe.IsNullRef(ref meta))
            {
                if (meta.Links.Length == 0)
                    meta.Links = Array.Empty<Key>();

                if (meta.Childs.Length == 0)
                    meta.Childs = Array.Empty<Key>();
            }
        }

        //Сжатие словарей
        Entities.TrimExcess();
        WordsIdsByNgramms.TrimExcess();
        EntitiesByWordsIndex.Trim();

        if (gcCompactLOH)
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        }
    }
}
