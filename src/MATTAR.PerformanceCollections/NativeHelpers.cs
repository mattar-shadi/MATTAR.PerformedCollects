using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

internal static unsafe class NativeHelpers
{
    private static readonly ThreadLocal<Random> _threadRandom = new(() => new Random());

    internal static void* AlignedAlloc(nuint size, nuint alignment = 64)
        => NativeMemory.AlignedAlloc(size, alignment);

    internal static void AlignedFree(void* ptr)
        => NativeMemory.AlignedFree(ptr);

    internal static void Clear(void* ptr, nuint size)
        => NativeMemory.Clear(ptr, size);

    internal static ulong RandomULong() => (ulong)_threadRandom.Value!.NextInt64();

    internal static ulong RandomOddULong() => ((ulong)_threadRandom.Value!.NextInt64() << 1) | 1UL;

    internal static int NextPowerOfTwo(int n)
    {
        if (n <= 0) return 1;
        n--;
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        return n + 1;
    }

    internal static int Log2(uint value) => BitOperations.Log2(value);
}
