using System.Runtime.InteropServices;
using MessagePack;

namespace QuerySeek.Models;

/// <summary>
/// Информация о совпадении слова с сущностью (EntityId, WordPositionInName, NameType)
/// </summary>
[MessagePackObject]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct WordMatchMeta
{
    public WordMatchMeta() { }

    public WordMatchMeta(
        int entityId,
        byte nameWordPosition,
        byte nameType)
    {
        EntityId = entityId;
        NameWordPosition = nameWordPosition;
        NameType = nameType;
    }

    [Key(1)]
    public int EntityId { get; }

    [Key(2)]
    public byte NameWordPosition { get; }

    [Key(3)]
    public byte NameType {  get; }
}
