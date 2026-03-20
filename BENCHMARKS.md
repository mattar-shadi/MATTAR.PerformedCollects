# Benchmarks

This document explains how to run the comparative benchmarks that measure the performance of the collections in this repository against their .NET standard equivalents (`Dictionary<TKey, TValue>` and `HashSet<T>`).

---

## Collections under test

| Repo collection | .NET equivalent | Mode |
|---|---|---|
| `CuckooHashTable` | `Dictionary<int, int>` | Dynamic (insertions, lookups, deletes) |
| `CuckooHashTable` | `HashSet<int>` | Set (key-only, value unused) |
| `PerfectHashTable` | `Dictionary<int, int>` | Static (construction + lookups only; no inserts/deletes after build) |

---

## Running the benchmarks

> **Requirement:** .NET 8 SDK

BenchmarkDotNet **must** run in `Release` configuration to produce meaningful numbers. Debug mode disables all JIT optimisations and gives misleading results.

### Run all benchmarks

```bash
dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks
```

### Filter by benchmark class

```bash
# Only the CuckooHashTable vs Dictionary benchmarks
dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks -- --filter *CuckooVsDictionary*

# Only the CuckooHashTable vs HashSet benchmarks
dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks -- --filter *CuckooVsHashSet*

# Only the PerfectHashTable vs Dictionary benchmarks
dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks -- --filter *PerfectVsDictionary*
```

### Filter by benchmark category

BenchmarkDotNet also supports `--filter` on method names:

```bash
# Only insertion benchmarks
dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks -- --filter *Insert* *Add* *Build*

# Only lookup benchmarks
dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks -- --filter *TryGetValue* *Find* *Contains*
```

### Export results

BenchmarkDotNet writes HTML, CSV and Markdown reports to `BenchmarkDotNet.Artifacts/` in the working directory:

```bash
dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks -- --exporters html markdown csv
```

---

## Benchmark scenarios

### `CuckooVsDictionaryBenchmarks`

Compares **CuckooHashTable** (native cuckoo hash, key→value) against `Dictionary<int, int>`.

| Method | Description |
|---|---|
| `Dictionary_Add` / `Cuckoo_Insert` | Build the entire table from N keys (measures construction + fill cost) |
| `Dictionary_TryGetValue` / `Cuckoo_Find` | Look up all N keys and accumulate values |
| `Dictionary_ContainsKey` / `Cuckoo_ContainsKey` | Test membership for all N keys |
| `Dictionary_Iterate` / `Cuckoo_Iterate` | Iterate over all entries and accumulate values |
| `Dictionary_Remove` / `Cuckoo_Delete` | Remove all N keys (uses `[IterationSetup]` to rebuild each time) |

### `CuckooVsHashSetBenchmarks`

Compares **CuckooHashTable used as a set** against `HashSet<int>`.

| Method | Description |
|---|---|
| `HashSet_Add` / `Cuckoo_Add` | Build the entire set from N keys |
| `HashSet_Contains` / `Cuckoo_Contains` | Test membership for all N keys |
| `HashSet_Iterate` / `Cuckoo_Iterate` | Iterate over all entries |
| `HashSet_Remove` / `Cuckoo_Remove` | Remove all N keys |

### `PerfectVsDictionaryBenchmarks`

Compares **PerfectHashTable** (static FKS perfect hash, key→value) against `Dictionary<int, int>`. Because `PerfectHashTable` is immutable after construction, there is no `Remove` benchmark.

| Method | Description |
|---|---|
| `Dictionary_Build` / `PerfectHashTable_Build` | Build the entire table from N key-value pairs |
| `Dictionary_TryGetValue` / `PerfectHashTable_Find` | Look up all N keys and accumulate values |
| `Dictionary_ContainsKey` / `PerfectHashTable_ContainsKey` | Test membership for all N keys |
| `Dictionary_Iterate` / `PerfectHashTable_Iterate` | Iterate over all entries |

---

## Parameters

All benchmark classes are parameterised over `N ∈ {100, 1_000, 10_000, 100_000}`.

Data is generated deterministically with a fixed seed (42) so results are reproducible across runs.

---

## Memory diagnostics

All benchmarks include `[MemoryDiagnoser]`, which reports:

| Column | Meaning |
|---|---|
| `Allocated` | Total managed heap bytes allocated during the benchmark (GC pressure) |
| `Gen0` / `Gen1` / `Gen2` | GC collection counts |

`CuckooHashTable` and `PerfectHashTable` allocate native memory directly via `NativeMemory` and do not generate GC pressure for the table storage itself, so their `Allocated` values will be significantly lower than their `Dictionary` / `HashSet` counterparts.

---

## Interpreting results

BenchmarkDotNet outputs a table like:

```
| Method              | N      | Mean       | Ratio | Allocated |
|---------------------|--------|------------|-------|-----------|
| Dictionary_Add      | 10000  | 1,234.5 us |  1.00 |  500.0 KB |
| Cuckoo_Insert       | 10000  |   987.3 us |  0.80 |    1.0 KB |
```

- **Mean**: average execution time per benchmark invocation.
- **Ratio**: time relative to the `[Benchmark(Baseline = true)]` method (< 1 = faster).
- **Allocated**: bytes allocated on the managed heap per invocation.

---

## Key constraints

- `CuckooHashTable` and `PerfectHashTable` use **key 0 as an empty-slot sentinel**; keys start at 1 in all benchmarks.
- `PerfectHashTable` is **immutable** after `Create`; Insert and Delete are not available after construction.
- Both native structures require `AllowUnsafeBlocks=true` in the consuming project.
