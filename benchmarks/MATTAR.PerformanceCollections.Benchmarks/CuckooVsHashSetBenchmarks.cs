using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace MATTAR.PerformanceCollections.Benchmarks;

/// <summary>
/// Compares <see cref="CuckooHashTable"/> used as a set (key-only, value ignored)
/// against <see cref="HashSet{T}"/> for the most common set operations.
///
/// Key constraint: CuckooHashTable uses key == 0 as an empty-slot sentinel,
/// so all generated keys start at 1.
/// </summary>
[MemoryDiagnoser]
public unsafe class CuckooVsHashSetBenchmarks
{
    // -------------------------------------------------------------------------
    // Parameters
    // -------------------------------------------------------------------------

    [Params(100, 1_000, 10_000, 100_000)]
    public int N;

    // -------------------------------------------------------------------------
    // Shared state
    // -------------------------------------------------------------------------

    private int[] _keys = null!;
    private int[] _lookupKeys = null!;

    // Pre-built structures for lookup / iteration / delete benchmarks.
    private CuckooHashTable* _cuckoo;
    private HashSet<int> _hashSet = null!;

    // -------------------------------------------------------------------------
    // Setup / Cleanup
    // -------------------------------------------------------------------------

    [GlobalSetup]
    public void Setup()
    {
        // Generate N deterministic keys starting at 1 (key 0 is reserved).
        var rng = new System.Random(42);
        var keySet = new HashSet<int>(N);
        _keys = new int[N];
        int k = 0;
        while (k < N)
        {
            int candidate = rng.Next(1, int.MaxValue);
            if (keySet.Add(candidate))
                _keys[k++] = candidate;
        }

        _lookupKeys = (int[])_keys.Clone();
        Shuffle(_lookupKeys, new System.Random(99));

        // Pre-built structures.
        _cuckoo = CuckooHashTable.Create(N);
        foreach (int key in _keys)
            CuckooHashTable.Insert(_cuckoo, key, 0);

        _hashSet = new HashSet<int>(N);
        foreach (int key in _keys)
            _hashSet.Add(key);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        CuckooHashTable.Destroy(_cuckoo);
        _cuckoo = null;
    }

    // -------------------------------------------------------------------------
    // Add
    // -------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void HashSet_Add()
    {
        var hs = new HashSet<int>(N);
        for (int i = 0; i < N; i++)
            hs.Add(_keys[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void Cuckoo_Add()
    {
        CuckooHashTable* tbl = CuckooHashTable.Create(N);
        for (int i = 0; i < N; i++)
            CuckooHashTable.Insert(tbl, _keys[i], 0);
        CuckooHashTable.Destroy(tbl);
    }

    // -------------------------------------------------------------------------
    // Contains
    // -------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int HashSet_Contains()
    {
        int count = 0;
        for (int i = 0; i < N; i++)
            if (_hashSet.Contains(_lookupKeys[i])) count++;
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int Cuckoo_Contains()
    {
        int count = 0;
        for (int i = 0; i < N; i++)
            if (CuckooHashTable.Find(_cuckoo, _lookupKeys[i]) != null) count++;
        return count;
    }

    // -------------------------------------------------------------------------
    // Iteration (foreach)
    // -------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Iterate")]
    public long HashSet_Iterate()
    {
        long sum = 0;
        foreach (int v in _hashSet)
            sum += v;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Iterate")]
    public long Cuckoo_Iterate()
    {
        long sum = 0;
        int size = _cuckoo->Size;
        CuckooHashTable.Entry* t1 = _cuckoo->Table1;
        CuckooHashTable.Entry* t2 = _cuckoo->Table2;
        for (int i = 0; i < size; i++)
        {
            ref CuckooHashTable.Entry e1 = ref t1[i];
            if (e1.Key != 0 && !e1.IsTombstone) sum += e1.Key;
            ref CuckooHashTable.Entry e2 = ref t2[i];
            if (e2.Key != 0 && !e2.IsTombstone) sum += e2.Key;
        }
        return sum;
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    private CuckooHashTable* _cuckooForDelete;
    private HashSet<int> _hashSetForDelete = null!;

    [IterationSetup(Targets = new[] { nameof(HashSet_Remove), nameof(Cuckoo_Remove) })]
    public void DeleteSetup()
    {
        _cuckooForDelete = CuckooHashTable.Create(N);
        foreach (int key in _keys)
            CuckooHashTable.Insert(_cuckooForDelete, key, 0);

        _hashSetForDelete = new HashSet<int>(_keys);
    }

    [IterationCleanup(Targets = new[] { nameof(HashSet_Remove), nameof(Cuckoo_Remove) })]
    public void DeleteCleanup()
    {
        CuckooHashTable.Destroy(_cuckooForDelete);
        _cuckooForDelete = null;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void HashSet_Remove()
    {
        for (int i = 0; i < N; i++)
            _hashSetForDelete.Remove(_lookupKeys[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void Cuckoo_Remove()
    {
        for (int i = 0; i < N; i++)
            CuckooHashTable.Delete(_cuckooForDelete, _lookupKeys[i]);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static void Shuffle(int[] arr, System.Random rng)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}
