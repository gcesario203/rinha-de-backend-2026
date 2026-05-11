using Microsoft.Extensions.Logging;

using AntiFraud.Core.BallTree.Entities;

namespace AntiFraud.API.Services;

/// <summary>
/// Persiste / carrega <c>references.balltree.bin</c> para evitar rebuild O(N log N) em cada cold start.
/// </summary>
public static class BallTreeCacheMaterializer
{
    /// <summary>Tenta carregar cache válido para o dataset atual (mesmo leafSize e tamanho do references.bin).</summary>
    public static BallTreeEntity? TryLoad(string cachePath, string referencesBinPath, IBallTreeDataSource dataSource, int leafSize)
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

        if (!BallTreeBinary.IsValidCacheFile(cachePath, leafSize, refLen))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(cachePath);
            return BallTreeEntity.LoadFromCache(bytes, dataSource, leafSize, refLen);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Escreve árvore em disco de forma atómica (tmp + move).</summary>
    public static void SaveAtomic(string cachePath, BallTreeEntity tree, int leafSize, long referencesBinLength, ILogger logger)
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
        logger.LogInformation("[DatasetLoader] Wrote ball-tree cache {Path} ({Bytes} bytes).", cachePath,
            new FileInfo(cachePath).Length);
    }
}
