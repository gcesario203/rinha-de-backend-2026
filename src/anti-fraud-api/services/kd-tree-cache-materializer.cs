using Microsoft.Extensions.Logging;

using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.KdTree.Entities;

namespace AntiFraud.API.Services;

/// <summary>Persiste / carrega <c>references.kdtree.bin</c> para evitar rebuild em cada cold start.</summary>
public static class KdTreeCacheMaterializer
{
    public static KdTreeEntity? TryLoad(string cachePath, string referencesBinPath, IBallTreeDataSource dataSource, int leafSize)
    {
        long refLen;
        try
        {
            refLen = new FileInfo(referencesBinPath).Length;
        }
        catch
        {
            return null;
        }

        if (!KdTreeBinary.IsValidCacheFile(cachePath, leafSize, refLen))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(cachePath);
            return KdTreeEntity.LoadFromCache(bytes, dataSource, leafSize, refLen);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveAtomic(string cachePath, KdTreeEntity tree, int leafSize, long referencesBinLength, ILogger logger)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(cachePath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmpPath = cachePath + ".tmp";
        if (File.Exists(tmpPath))
        {
            try { File.Delete(tmpPath); } catch (IOException) { }
        }

        using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20,
                   FileOptions.SequentialScan))
        {
            tree.WriteFullCache(fs, leafSize, referencesBinLength);
        }

        File.Move(tmpPath, cachePath, overwrite: true);
        logger.LogInformation("[DatasetLoader] Wrote KD-tree cache {Path} ({Bytes} bytes).", cachePath,
            new FileInfo(cachePath).Length);
    }
}
