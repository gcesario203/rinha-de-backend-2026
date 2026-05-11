using System.Buffers.Binary;

using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Core.BallTree.Entities;

/// <summary>Header do ficheiro <c>references.balltree.bin</c> (cache da árvore pré-construída).</summary>
public static class BallTreeBinary
{
    public static ReadOnlySpan<byte> Magic => "BTRE"u8;

    /// <summary>v2: persistir centróide+raio também em folhas (v1 cacheava árvore com pruning incorreto).</summary>
    public const uint CurrentVersion = 2;

    /// <summary>Tamanho fixo do header (padding para evolução).</summary>
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

    /// <summary>Valida header + tamanho mínimo do ficheiro (corpo não-empty).</summary>
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
