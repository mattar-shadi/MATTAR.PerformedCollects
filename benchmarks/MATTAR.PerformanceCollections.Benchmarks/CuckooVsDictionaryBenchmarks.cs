using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace MATTAR.PerformanceCollections.Benchmarks;

/// <summary>
/// Compares <see cref="CuckooHashTable"/> (native, unsafe, key→value map) against
/// <see cref="Dictionary{TKey,TValue}"/> for the most common operations.
///
/// Key constraint: CuckooHashTable uses key == 0 as an empty-slot sentinel,
/// so all generated keys start at 1.
/// </summary>
[MemoryDiagnoser]
public unsafe class CuckooVsDictionaryBenchmarks
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

    // State for lookup / iteration / delete benchmarks (pre-built structures).
    private CuckooHashTable* _cuckoo;
    private Dictionary<int, int> _dictionary = null!;

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

        // A separate shuffled copy used for lookup / delete to avoid
        // sequential-access bias.
        _lookupKeys = (int[])_keys.Clone();
        Shuffle(_lookupKeys, new System.Random(99));

        // Pre-build structures used by the lookup/iterate/delete benchmarks.
        _cuckoo = CuckooHashTable.Create(N);
        foreach (int key in _keys)
            CuckooHashTable.Insert(_cuckoo, key, key);

        _dictionary = new Dictionary<int, int>(N);
        foreach (int key in _keys)
            _dictionary.Add(key, key);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        CuckooHashTable.Destroy(_cuckoo);
        _cuckoo = null;
    }

    // -------------------------------------------------------------------------
    // Insert (Add)
    // -------------------------------------------------------------------------

    /// <summary>Creates a fresh CuckooHashTable and inserts all N keys.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public void Dictionary_Add()
    {
        var dict = new Dictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            dict.Add(_keys[i], _keys[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void Cuckoo_Insert()
    {
        CuckooHashTable* tbl = CuckooHashTable.Create(N);
        for (int i = 0; i < N; i++)
            CuckooHashTable.Insert(tbl, _keys[i], _keys[i]);
        CuckooHashTable.Destroy(tbl);
    }

    // -------------------------------------------------------------------------
    // TryGetValue / Find
    // -------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TryGetValue")]
    public int Dictionary_TryGetValue()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
            if (_dictionary.TryGetValue(_lookupKeys[i], out int v))
                sum += v;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("TryGetValue")]
    public int Cuckoo_Find()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            CuckooHashTable.Entry* e = CuckooHashTable.Find(_cuckoo, _lookupKeys[i]);
            if (e != null) sum += e->Value;
        }
        return sum;
    }

    // -------------------------------------------------------------------------
    // ContainsKey
    // -------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ContainsKey")]
    public int Dictionary_ContainsKey()
    {
        int count = 0;
        for (int i = 0; i < N; i++)
            if (_dictionary.ContainsKey(_lookupKeys[i])) count++;
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("ContainsKey")]
    public int Cuckoo_ContainsKey()
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
    public long Dictionary_Iterate()
    {
        long sum = 0;
        foreach (var kvp in _dictionary)
            sum += kvp.Value;
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
            if (e1.Key != 0 && !e1.IsTombstone) sum += e1.Value;
            ref CuckooHashTable.Entry e2 = ref t2[i];
            if (e2.Key != 0 && !e2.IsTombstone) sum += e2.Value;
        }
        return sum;
    }

    // -------------------------------------------------------------------------
    // Remove (Delete)
    // -------------------------------------------------------------------------

    // For delete, we rebuild the structures per iteration so each benchmark
    // call starts with a full table.

    private CuckooHashTable* _cuckooForDelete;
    private Dictionary<int, int> _dictForDelete = null!;

    [IterationSetup(Targets = new[] { nameof(Dictionary_Remove), nameof(Cuckoo_Delete) })]
    public void DeleteSetup()
    {
        _cuckooForDelete = CuckooHashTable.Create(N);
        foreach (int key in _keys)
            CuckooHashTable.Insert(_cuckooForDelete, key, key);

        _dictForDelete = new Dictionary<int, int>(N);
        foreach (int key in _keys)
            _dictForDelete.Add(key, key);
    }

    [IterationCleanup(Targets = new[] { nameof(Dictionary_Remove), nameof(Cuckoo_Delete) })]
    public void DeleteCleanup()
    {
        CuckooHashTable.Destroy(_cuckooForDelete);
        _cuckooForDelete = null;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        for (int i = 0; i < N; i++)
            _dictForDelete.Remove(_lookupKeys[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void Cuckoo_Delete()
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
