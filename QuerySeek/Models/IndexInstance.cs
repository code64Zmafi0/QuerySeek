using System.Runtime;
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

    public void Trim()
    {
        foreach (EntityMeta meta in Entities.Values)
        {
            if (meta.Links.Length == 0)
                meta.Links = Array.Empty<Key>();

            if (meta.Childs.Length == 0)
                meta.Childs = Array.Empty<Key>();
        }

        Entities.TrimExcess();
        WordsIdsByNgramms.TrimExcess();
        EntitiesByWordsIndex.Trim();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
    }
}
