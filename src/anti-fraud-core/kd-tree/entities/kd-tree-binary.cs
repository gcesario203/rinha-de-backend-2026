using System.Buffers.Binary;

using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Core.KdTree.Entities;

/// <summary>Header do ficheiro <c>references.kdtree.bin</c> (cache da árvore pré-construída).</summary>
public static class KdTreeBinary
{
    public static ReadOnlySpan<byte> Magic => "KDTR"u8;

    public const uint CurrentVersion = 1;

    /// <summary>Tamanho fixo do header (64 bytes, igual ao da ball-tree).</summary>
    public const int HeaderSize = 64;

    public static void WriteHeader(Span<byte> header, int leafSize, long referencesBinLength)
    {
        if (header.Length < HeaderSize)
            throw new ArgumentException(null, nameof(header));

        header.Clear();
        Magic.CopyTo(header.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), CurrentVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), leafSize);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), VectorDatasetConstants.ReferenceCount);
        BinaryPrimitives.WriteInt64LittleEndian(header.Slice(16, 8), referencesBinLength);
    }

    public static bool TryValidateHeader(ReadOnlySpan<byte> header, int expectedLeafSize, long referencesBinLength, out int referenceCount)
    {
        referenceCount = 0;
        if (header.Length < HeaderSize)
            return false;

        if (!header.Slice(0, 4).SequenceEqual(Magic))
            return false;

        if (BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4)) != CurrentVersion)
            return false;

        var leafSize = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(8, 4));
        if (leafSize != expectedLeafSize)
            return false;

        referenceCount = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(12, 4));
        if (referenceCount != VectorDatasetConstants.ReferenceCount)
            return false;

        var storedLen = BinaryPrimitives.ReadInt64LittleEndian(header.Slice(16, 8));
        if (storedLen != referencesBinLength)
            return false;

        return true;
    }

    public static bool IsValidCacheFile(string cachePath, int leafSize, long referencesBinLength)
    {
        try
        {
            var fi = new FileInfo(cachePath);
            if (fi.Length <= HeaderSize)
                return false;

            Span<byte> header = stackalloc byte[HeaderSize];
            using var fs = File.OpenRead(cachePath);
            fs.ReadExactly(header);

            return TryValidateHeader(header, leafSize, referencesBinLength, out _);
        }
        catch
        {
            return false;
        }
    }
}
