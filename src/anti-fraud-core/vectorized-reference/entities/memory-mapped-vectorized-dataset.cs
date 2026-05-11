using System.IO.MemoryMappedFiles;
using AntiFraud.Core.BallTree.Entities;

namespace AntiFraud.Core.VectorizedReference.Entities;

/// <summary>
/// Read-only vector dataset backed by a memory-mapped file (kernel shares physical pages across processes).
/// </summary>
public sealed unsafe class MemoryMappedVectorizedDataset : IBallTreeDataSource, IDisposable
{
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private byte* _ptr;
    private bool _pointerHeld;
    private int _count;
    private long _vectorsByteOffset;
    private long _labelsByteOffset;

    public int Count => _count;

    /// <summary>Throws if the file header or length does not match the expected layout.</summary>
    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        DisposeMapping();

        _mmf = MemoryMappedFile.CreateFromFile(
            path,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read);

        _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
        _pointerHeld = true;

        var fi = new FileInfo(path);
        if (fi.Length != VectorDatasetBinary.ExpectedFileLength())
            throw new InvalidDataException($"Unexpected file size for vector dataset: {fi.Length}.");

        Span<byte> header = stackalloc byte[VectorDatasetConstants.HeaderSize];
        new Span<byte>(_ptr, VectorDatasetConstants.HeaderSize).CopyTo(header);

        if (!VectorDatasetBinary.TryValidateHeader(header, out var parsedCount))
            throw new InvalidDataException("Invalid vector dataset header.");

        _count = parsedCount;
        _vectorsByteOffset = VectorDatasetConstants.HeaderSize;
        _labelsByteOffset = _vectorsByteOffset + (long)_count * VectorDatasetConstants.Dimensions * sizeof(float);
    }

    /// <summary>
    /// Toca uma vez todas as páginas do mmap em ordem sequencial.
    /// Custa O(arquivo) na inicialização e elimina major page-faults durante o hot path.
    /// </summary>
    public long PreFault()
    {
        if (_ptr is null) return 0;

        var totalBytes = _labelsByteOffset + (long)_count;
        // 4 KiB é o tamanho de página no Linux para arquiteturas amd64/arm64 padrão.
        const int PageSize = 4096;

        // Soma um byte por página para evitar que o JIT/otimizador descarte a leitura.
        long checksum = 0;
        for (long offset = 0; offset < totalBytes; offset += PageSize)
        {
            checksum += *(_ptr + offset);
        }
        return checksum;
    }

    public ReadOnlySpan<float> GetVectorSpan(int index)
    {
        var offset = _vectorsByteOffset + (long)index * VectorDatasetConstants.Dimensions * sizeof(float);
        return new Span<float>(_ptr + offset, VectorDatasetConstants.Dimensions);
    }

    public bool GetLabel(int index)
    {
        return *(_ptr + _labelsByteOffset + index) != 0;
    }

    public void Dispose()
    {
        DisposeMapping();
    }

    private void DisposeMapping()
    {
        if (_pointerHeld && _accessor is not null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _pointerHeld = false;
            _ptr = null;
        }

        _accessor?.Dispose();
        _accessor = null;
        _mmf?.Dispose();
        _mmf = null;
        _count = 0;
    }
}
