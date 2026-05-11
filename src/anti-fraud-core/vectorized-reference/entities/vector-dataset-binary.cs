using System.Buffers.Binary;

namespace AntiFraud.Core.VectorizedReference.Entities;

public static class VectorDatasetBinary
{
    public static ReadOnlySpan<byte> Magic => "RFVD"u8;

    public const uint CurrentVersion = 1;

    public static long ExpectedFileLength()
    {
        var n = VectorDatasetConstants.ReferenceCount;
        return VectorDatasetConstants.HeaderSize
            + (long)n * VectorDatasetConstants.Dimensions * sizeof(float)
            + n;
    }

    public static bool TryValidateHeader(ReadOnlySpan<byte> header, out int count)
    {
        count = 0;
        if (header.Length < VectorDatasetConstants.HeaderSize)
            return false;

        if (!header.Slice(0, 4).SequenceEqual(Magic))
            return false;

        var version = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4));
        if (version != CurrentVersion)
            return false;

        count = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(8, 4));
        if (count != VectorDatasetConstants.ReferenceCount)
            return false;

        var dimensions = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(12, 4));
        if (dimensions != VectorDatasetConstants.Dimensions)
            return false;

        return true;
    }

    public static bool IsValidFile(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (fi.Length != ExpectedFileLength())
                return false;

            Span<byte> header = stackalloc byte[VectorDatasetConstants.HeaderSize];
            using var fs = File.OpenRead(path);
            fs.ReadExactly(header);

            return TryValidateHeader(header, out _);
        }
        catch
        {
            return false;
        }
    }

    public static void WriteHeader(Span<byte> header)
    {
        Magic.CopyTo(header.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), CurrentVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), VectorDatasetConstants.ReferenceCount);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), VectorDatasetConstants.Dimensions);
        header.Slice(16).Clear();
    }
}
