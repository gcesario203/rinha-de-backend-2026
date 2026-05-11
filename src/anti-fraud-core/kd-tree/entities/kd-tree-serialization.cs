using System.Buffers.Binary;

using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Core.KdTree.Entities;

/// <summary>Preorder após <see cref="KdTreeBinary.HeaderSize"/>: folha ou interno com eixo + valor de corte.</summary>
public static class KdTreeSerialization
{
    private const byte TagInternal = 0;
    private const byte TagLeaf = 1;

    public static void WriteTree(Stream stream, KdTreeNodeEntity root)
    {
        WriteNode(stream, root);
    }

    private static void WriteNode(Stream stream, KdTreeNodeEntity node)
    {
        if (node.IsLeaf)
        {
            stream.WriteByte(TagLeaf);
            var indices = node.PointIndices!;
            WriteInt32(stream, indices.Length);
            foreach (var idx in indices)
                WriteInt32(stream, idx);
            return;
        }

        stream.WriteByte(TagInternal);
        stream.WriteByte(node.SplitDim);
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.WriteByte(0);
        WriteSingle(stream, node.SplitValue);
        WriteNode(stream, node.Left!);
        WriteNode(stream, node.Right!);
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

    public static KdTreeNodeEntity ReadTree(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset >= span.Length)
            throw new EndOfStreamException("KD-tree cache truncated.");

        var tag = span[offset++];
        return tag switch
        {
            TagLeaf => ReadLeaf(span, ref offset),
            TagInternal => ReadInternal(span, ref offset),
            _ => throw new InvalidDataException($"Unknown KD-tree node tag {tag}."),
        };
    }

    private static KdTreeNodeEntity ReadLeaf(ReadOnlySpan<byte> span, ref int offset)
    {
        var len = ReadInt32(span, ref offset);
        if (len < 0 || len > VectorDatasetConstants.ReferenceCount)
            throw new InvalidDataException($"Invalid KD-tree leaf length {len}.");

        var arr = new int[len];
        for (var i = 0; i < len; i++)
            arr[i] = ReadInt32(span, ref offset);

        return new KdTreeNodeEntity { PointIndices = arr };
    }

    private static KdTreeNodeEntity ReadInternal(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 1 + 3 + 4 > span.Length)
            throw new EndOfStreamException();

        var dim = span[offset++];
        if (dim >= VectorDatasetConstants.Dimensions)
            throw new InvalidDataException($"Invalid split dimension {dim}.");

        offset += 3; // padding após 1 byte de dimensão
        var split = ReadSingle(span, ref offset);

        var node = new KdTreeNodeEntity
        {
            SplitDim = dim,
            SplitValue = split,
            Left = ReadTree(span, ref offset),
            Right = ReadTree(span, ref offset),
        };
        return node;
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
