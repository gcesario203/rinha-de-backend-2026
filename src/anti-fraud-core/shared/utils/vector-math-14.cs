using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AntiFraud.Core.Shared.Utils;

/// <summary>
/// Distâncias euclidianas para vetores de 14 dimensões usando SIMD (Vector128 = 4 lanes).
/// Layout: 3 blocos vetoriais (i=0..11) + 2 escalares (i=12,13).
/// Vector128 é suportado em x86_64 (SSE2) e ARM64 (Neon) — portátil em qualquer runtime moderno.
/// </summary>
public static class VectorMath14
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        ref float ra = ref Unsafe.AsRef(in MemoryMarshal.GetReference(a));
        ref float rb = ref Unsafe.AsRef(in MemoryMarshal.GetReference(b));

        var d0 = Vector128.LoadUnsafe(ref ra) - Vector128.LoadUnsafe(ref rb);
        var d1 = Vector128.LoadUnsafe(ref ra, 4) - Vector128.LoadUnsafe(ref rb, 4);
        var d2 = Vector128.LoadUnsafe(ref ra, 8) - Vector128.LoadUnsafe(ref rb, 8);

        var sumVec = (d0 * d0) + (d1 * d1) + (d2 * d2);
        var total = Vector128.Sum(sumVec);

        var e12 = Unsafe.Add(ref ra, 12) - Unsafe.Add(ref rb, 12);
        var e13 = Unsafe.Add(ref ra, 13) - Unsafe.Add(ref rb, 13);
        total += e12 * e12 + e13 * e13;
        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        => MathF.Sqrt(DistanceSquared(a, b));

    /// <summary>
    /// Produto interno <c>a · b</c> em 14-D. Custa metade de <see cref="DistanceSquared"/> e é usado
    /// na partição do ball-tree (split por hiperplano: <c>p · delta ≤ threshold</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        ref float ra = ref Unsafe.AsRef(in MemoryMarshal.GetReference(a));
        ref float rb = ref Unsafe.AsRef(in MemoryMarshal.GetReference(b));

        var p0 = Vector128.LoadUnsafe(ref ra) * Vector128.LoadUnsafe(ref rb);
        var p1 = Vector128.LoadUnsafe(ref ra, 4) * Vector128.LoadUnsafe(ref rb, 4);
        var p2 = Vector128.LoadUnsafe(ref ra, 8) * Vector128.LoadUnsafe(ref rb, 8);

        var sumVec = p0 + p1 + p2;
        var total = Vector128.Sum(sumVec);
        total += Unsafe.Add(ref ra, 12) * Unsafe.Add(ref rb, 12);
        total += Unsafe.Add(ref ra, 13) * Unsafe.Add(ref rb, 13);
        return total;
    }

    /// <summary>Norma euclidiana ao quadrado (<c>||a||²</c>).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float NormSquared(ReadOnlySpan<float> a) => DotProduct(a, a);

    /// <summary>
    /// Software prefetch da linha de cache que contém o vetor (T0 = L1).
    /// No-op se SSE não estiver disponível (raro em x86_64).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Prefetch(ReadOnlySpan<float> span)
    {
        if (Sse.IsSupported)
        {
            ref float r = ref Unsafe.AsRef(in MemoryMarshal.GetReference(span));
            Sse.Prefetch0(Unsafe.AsPointer(ref r));
        }
    }
}
