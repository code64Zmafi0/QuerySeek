using System.Runtime.InteropServices;
using MessagePack;

namespace QuerySeek.Models;

[MessagePackObject]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct NgrammAssociation(int wordId, byte position)
{
    [Key(1)]
    public readonly int WordId = wordId;

    [Key(2)]
    public readonly byte Position = position;
}
