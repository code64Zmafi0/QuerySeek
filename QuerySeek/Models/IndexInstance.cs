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

    /// <summary>
    /// Информация для сущностей о линках и потомках
    /// </summary>
    [Key(1)]
    public Dictionary<Key, EntityMeta> Entities { get; internal set; } = [];

    /// <summary>
    /// ID слов по хешу нграмма
    /// </summary>
    [Key(2)]
    public Dictionary<int, int[]> WordsIdsByNgramms { get; internal set; } = [];

    /// <summary>
    /// Словарь WordId -> Types -> Containers -> MatchesToEntites
    /// Так как ид слов последовательны использован массив вместо словаря - так как словарь большого размера в разы медленней на обращени и занимает больше места
    /// </summary>
    [Key(3)]
    public KeyValuePair<byte /*TypeId*/, Dictionary</*ContainerKey*/ Key, WordMatchMeta[]>>[][/*WordId*/] EntitiesSearchMap { get; set; } = [];

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
            if (Entities.TryGetValue(key, out EntityMeta? meta))
            {
                if (meta.Links.Length == 0)
                    meta.Links = Array.Empty<Key>();

                if (meta.Childs.Length == 0)
                    meta.Childs = Array.Empty<Key>();
            }
        }

        //Сжимаем словари поисковой мапы
        foreach (KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>>[] collection in EntitiesSearchMap)
        {
            foreach (KeyValuePair<byte, Dictionary<Key, WordMatchMeta[]>> item in collection)
            {
                item.Value.TrimExcess();
            }
        }

        //Сжатие словарей
        Entities.TrimExcess();
        WordsIdsByNgramms.TrimExcess();

        if (gcCompactLOH)
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        }
    }
}
