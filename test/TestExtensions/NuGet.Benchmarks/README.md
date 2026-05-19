# NuGet.Benchmarks

A micro-benchmark project based on [BenchmarkDotNet](https://benchmarkdotnet.org/) for quick performance experiments inside the NuGet.Client repository.

## Purpose

This project exists to make it easy to write and run micro-benchmarks against NuGet source code without having to set up a separate repository or solution. It is intended for **local use only** — benchmark classes should generally not be checked in. Use it for:

- Profiling allocations and throughput while developing a feature or fix.
- Comparing the performance of two implementation approaches before committing.
- Reproducing a performance regression reported in an issue.

## How to run

```bash
dotnet run -c Release --project test/TestExtensions/NuGet.Benchmarks/NuGet.Benchmarks.csproj
```

> **Note:** Always run benchmarks in `Release` configuration. Debug / unoptimised builds produce misleading results.

## Writing benchmarks

1. Add a public class to `Program.cs` (or a new `.cs` file in this project) and annotate benchmark methods with `[Benchmark]`.
2. Update the `BenchmarkRunner.Run<T>()` call in `Program.cs` to point at your new class.
3. Run the project in Release mode (see above).

A simple example is already included in `Program.cs` to get you started.

For full documentation on available attributes (`[MemoryDiagnoser]`, `[Params]`, `[GlobalSetup]`, etc.) see the [BenchmarkDotNet docs](https://benchmarkdotnet.org/articles/overview.html).

## Prior art

This approach mirrors the benchmark project used in the .NET SDK:
<https://github.com/dotnet/dotnet/blob/main/src/sdk/benchmarks/MicroBenchmark/MicroBenchmark.csproj>
