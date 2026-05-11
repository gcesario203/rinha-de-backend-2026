using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;
using AntiFraud.Core.Serialization;
using AntiFraud.Core.VectorizedReference.Entities;
using AntiFraud.Core.VectorizedReference.Models;

namespace AntiFraud.API.Services;

public static class VectorDatasetMaterializer
{
    /// <summary>
    /// Ensures <paramref name="binPath"/> exists and matches the expected layout.
    /// If not, streams from <paramref name="gzPath"/> and writes the binary file atomically (temp + move).
    /// Uses an exclusive lock file so parallel startup (e.g. two Docker replicas on a shared volume) cannot corrupt the dataset.
    /// </summary>
    public static async Task EnsureBinaryReadyAsync(
        string gzPath,
        string binPath,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(binPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var lockPath = binPath + ".lock";

        await using (var lockStream = new FileStream(
                          lockPath,
                          FileMode.OpenOrCreate,
                          FileAccess.ReadWrite,
                          FileShare.None,
                          bufferSize: 4096,
                          FileOptions.Asynchronous))
        {
            if (VectorDatasetBinary.IsValidFile(binPath))
            {
                logger.LogInformation("[DatasetLoader] Using existing vector bin at {Path}.", binPath);
                return;
            }

            logger.LogInformation("[DatasetLoader] Materializing {BinPath} from {GzPath}...", binPath, gzPath);
            await MaterializeFromGzipAsync(gzPath, binPath, logger, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task MaterializeFromGzipAsync(
        string gzPath,
        string binPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var expected = VectorDatasetConstants.ReferenceCount;
        var labels = new byte[expected];
        var tmpPath = binPath + ".tmp";

        if (File.Exists(tmpPath))
        {
            try
            {
                File.Delete(tmpPath);
            }
            catch (IOException)
            {
                // Competing writer; creation will fail if still locked.
            }
        }

        const int VectorBytes = VectorDatasetConstants.Dimensions * sizeof(float);

        await using (var outStream = new FileStream(
                          tmpPath,
                          FileMode.Create,
                          FileAccess.Write,
                          FileShare.None,
                          bufferSize: 1 << 20,
                          options: FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var headerBuffer = new byte[VectorDatasetConstants.HeaderSize];
            VectorDatasetBinary.WriteHeader(headerBuffer);
            await outStream.WriteAsync(headerBuffer, cancellationToken).ConfigureAwait(false);

            await using var fileStream = new FileStream(
                gzPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);

            var vectorBuffer = new byte[VectorBytes];
            var index = 0;

            await foreach (var record in JsonSerializer
                               .DeserializeAsyncEnumerable<VectorizedReferenceFileModel>(
                                   gzipStream,
                                   AntiFraudJsonSerializerContext.Default.VectorizedReferenceFileModel,
                                   cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (record is null)
                    continue;

                if (index >= expected)
                    throw new InvalidDataException($"More than {expected} records in {gzPath}.");

                if (record.Vector.Length != VectorDatasetConstants.Dimensions)
                    throw new InvalidDataException(
                        $"Record at index {index} has {record.Vector.Length} dimensions (expected {VectorDatasetConstants.Dimensions}).");

                labels[index] = record.Label.Equals("fraud", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)0;

                EncodeVector(record.Vector, vectorBuffer);
                await outStream.WriteAsync(vectorBuffer, cancellationToken).ConfigureAwait(false);

                index++;

                if (index % 100_000 == 0)
                    logger.LogInformation("[DatasetLoader] {Index} records serialized...", index);
            }

            if (index != expected)
                throw new InvalidDataException($"Expected {expected} records in {gzPath}, found {index}.");

            await outStream.WriteAsync(labels, cancellationToken).ConfigureAwait(false);
            await outStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(tmpPath, binPath, overwrite: true);
        logger.LogInformation("[DatasetLoader] Wrote {BinPath} ({Bytes} bytes).", binPath,
            new FileInfo(binPath).Length);
    }

    private static void EncodeVector(float[] vector, byte[] destination)
    {
        var span = destination.AsSpan();
        for (var i = 0; i < vector.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(i * sizeof(float), sizeof(float)), vector[i]);
    }
}
