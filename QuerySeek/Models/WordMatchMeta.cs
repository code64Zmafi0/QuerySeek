using MessagePack;

namespace QuerySeek.Models;

/// <summary>
/// Информация о совпадении слова с сущностью (EntityId, WordPositionInName, PhraseType)
/// </summary>
[MessagePackObject]
public readonly struct WordMatchMeta
{
    public WordMatchMeta() { }

    public WordMatchMeta(
        int entityId,
        byte nameWordPosition,
        byte phraseType)
    {
        EntityId = entityId;
        NameWordPosition = nameWordPosition;
        PhraseType = phraseType;
    }

    [Key(1)]
    public int EntityId { get; }

    [Key(2)]
    public byte NameWordPosition { get; }

    [Key(3)]
    public byte PhraseType {  get; }
}
