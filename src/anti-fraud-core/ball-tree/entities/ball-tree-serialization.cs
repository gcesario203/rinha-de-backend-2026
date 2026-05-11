using System.Buffers.Binary;

using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Core.BallTree.Entities;

/// <summary>
/// Formato preorder do corpo do cache (após <see cref="BallTreeBinary.HeaderSize"/> bytes).
/// TODOS os nós persistem centróide + raio — caso contrário a poda no <c>SearchNode</c>
/// usa um centróide zerado e devolve vizinhos errados.
/// <list type="bullet">
/// <item>Folha:   <c>0x01</c> + 14×float32 centróide + float32 raio + int32 count + count × int32 índices.</item>
/// <item>Interno: <c>0x00</c> + 14×float32 centróide + float32 raio + subárvore esq + subárvore dir.</item>
/// </list>
/// </summary>
public static class BallTreeSerialization
{
    private const byte TagInternal = 0;
    private const byte TagLeaf = 1;

    public static void WriteTree(Stream stream, BallTreeNodeEntity root)
    {
        WriteNode(stream, root);
    }

    private static void WriteNode(Stream stream, BallTreeNodeEntity node)
    {
        if (node.IsLeaf)
        {
            stream.WriteByte(TagLeaf);
            WriteCentroidAndRadius(stream, node);
            var indices = node.PointIndices!;
            WriteInt32(stream, indices.Length);
            foreach (var idx in indices)
                WriteInt32(stream, idx);
            return;
        }

        stream.WriteByte(TagInternal);
        WriteCentroidAndRadius(stream, node);
        WriteNode(stream, node.Left!);
        WriteNode(stream, node.Right!);
    }

    private static void WriteCentroidAndRadius(Stream stream, BallTreeNodeEntity node)
    {
        foreach (var v in node.Centroid)
            WriteSingle(stream, v);
        WriteSingle(stream, node.Radius);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, value);
        stream.Write(b);
    }

    private static void WriteSingle(Stream stream, float value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(b, value);
        stream.Write(b);
    }

    public static BallTreeNodeEntity ReadTree(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset >= span.Length)
            throw new EndOfStreamException("Ball-tree cache truncated.");

        var tag = span[offset++];
        return tag switch
        {
            TagLeaf => ReadLeaf(span, ref offset),
            TagInternal => ReadInternal(span, ref offset),
            _ => throw new InvalidDataException($"Unknown ball-tree node tag {tag}."),
        };
    }

    private static BallTreeNodeEntity ReadLeaf(ReadOnlySpan<byte> span, ref int offset)
    {
        var node = new BallTreeNodeEntity();
        ReadCentroidAndRadius(span, ref offset, node);

        var len = ReadInt32(span, ref offset);
        if (len < 0 || len > VectorDatasetConstants.ReferenceCount)
            throw new InvalidDataException($"Invalid leaf length {len}.");

        var arr = new int[len];
        for (var i = 0; i < len; i++)
            arr[i] = ReadInt32(span, ref offset);

        node.PointIndices = arr;
        return node;
    }

    private static BallTreeNodeEntity ReadInternal(ReadOnlySpan<byte> span, ref int offset)
    {
        var node = new BallTreeNodeEntity();
        ReadCentroidAndRadius(span, ref offset, node);
        node.Left = ReadTree(span, ref offset);
        node.Right = ReadTree(span, ref offset);
        return node;
    }

    private static void ReadCentroidAndRadius(ReadOnlySpan<byte> span, ref int offset, BallTreeNodeEntity node)
    {
        for (var d = 0; d < 14; d++)
            node.Centroid[d] = ReadSingle(span, ref offset);
        node.Radius = ReadSingle(span, ref offset);
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 4 > span.Length)
            throw new EndOfStreamException();

        var v = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
        offset += 4;
        return v;
    }

    private static float ReadSingle(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 4 > span.Length)
            throw new EndOfStreamException();

        var v = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4));
        offset += 4;
        return v;
    }
}
