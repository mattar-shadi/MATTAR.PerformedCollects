using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

// Run all benchmarks in the current assembly.
// Usage (from repo root):
//   dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks
//
// Filter to a specific group:
//   dotnet run -c Release --project benchmarks/MATTAR.PerformanceCollections.Benchmarks -- --filter *Dictionary*
//
// See BENCHMARKS.md for full usage instructions.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
