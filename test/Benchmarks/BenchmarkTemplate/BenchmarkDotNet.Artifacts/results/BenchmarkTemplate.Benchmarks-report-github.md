``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET Core SDK=5.0.300-preview.21228.15
  [Host]     : .NET Core 5.0.6 (CoreCLR 5.0.621.22011, CoreFX 5.0.621.22011), X64 RyuJIT
  DefaultJob : .NET Core 5.0.6 (CoreCLR 5.0.621.22011, CoreFX 5.0.621.22011), X64 RyuJIT


```
|  Method |      Mean |     Error |    StdDev |    Median |
|-------- |----------:|----------:|----------:|----------:|
| example | 0.0170 ns | 0.0172 ns | 0.0161 ns | 0.0118 ns |
