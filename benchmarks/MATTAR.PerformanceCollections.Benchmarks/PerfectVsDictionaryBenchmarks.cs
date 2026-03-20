using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace MATTAR.PerformanceCollections.Benchmarks;

/// <summary>
/// Compares <see cref="PerfectHashTable"/> (static FKS hash table, key→value map)
/// against <see cref="Dictionary{TKey,TValue}"/> for read-oriented operations.
///
/// <para>
/// <see cref="PerfectHashTable"/> is <em>immutable</em> after construction, so
/// insertion and removal benchmarks are replaced by a construction benchmark that
/// measures the full build cost (equivalent to populating a dictionary from scratch).
/// </para>
///
/// Key constraint: PerfectHashTable uses key == 0 as an empty-slot sentinel,
/// so all generated keys start at 1.
/// </summary>
[MemoryDiagnoser]
public unsafe class PerfectVsDictionaryBenchmarks
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
    private int[] _values = null!;
    private int[] _lookupKeys = null!;

    // Pre-built structures for lookup / iteration benchmarks.
    private PerfectHashTable* _perfect;
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

        _values = new int[N];
        for (int i = 0; i < N; i++) _values[i] = _keys[i];

        _lookupKeys = (int[])_keys.Clone();
        Shuffle(_lookupKeys, new System.Random(99));

        // Pre-built structures.
        _perfect = PerfectHashTable.Create(_keys, _values);

        _dictionary = new Dictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            _dictionary.Add(_keys[i], _values[i]);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        PerfectHashTable.Destroy(_perfect);
        _perfect = null;
    }

    // -------------------------------------------------------------------------
    // Build (construction)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Measures the full cost of building a Dictionary from N key-value pairs.
    /// This is the baseline for the construction benchmark.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public void Dictionary_Build()
    {
        var dict = new Dictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            dict.Add(_keys[i], _values[i]);
    }

    /// <summary>
    /// Measures the full cost of building a PerfectHashTable from N key-value pairs.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Build")]
    public void PerfectHashTable_Build()
    {
        PerfectHashTable* tbl = PerfectHashTable.Create(_keys, _values);
        PerfectHashTable.Destroy(tbl);
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
    public int PerfectHashTable_Find()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            PerfectHashTable.Entry* e = PerfectHashTable.Find(_perfect, _lookupKeys[i]);
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
    public int PerfectHashTable_ContainsKey()
    {
        int count = 0;
        for (int i = 0; i < N; i++)
            if (PerfectHashTable.Find(_perfect, _lookupKeys[i]) != null) count++;
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
    public long PerfectHashTable_Iterate()
    {
        long sum = 0;
        int tableSize = _perfect->TableSize;
        PerfectHashTable.Bucket* buckets = _perfect->Buckets;
        for (int b = 0; b < tableSize; b++)
        {
            ref PerfectHashTable.Bucket bucket = ref buckets[b];
            if (bucket.SubTableSize == 0) continue;
            PerfectHashTable.Entry* sub = bucket.SubTable;
            int subSize = bucket.SubTableSize;
            for (int j = 0; j < subSize; j++)
            {
                ref PerfectHashTable.Entry e = ref sub[j];
                if (e.Key != 0) sum += e.Value;
            }
        }
        return sum;
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
