```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.3880/23H2/2023Update/SunValley3) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100-preview.6.24328.19
  [Host]     : .NET 7.0.20 (7.0.2024.26716), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.20 (7.0.2024.26716), X64 RyuJIT AVX2


```
| Method                   | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| ConverterWithoutLoadJson | 19.16 μs | 0.134 μs | 0.125 μs | 0.8545 |      - |  21.18 KB |
| ConverterUsingLoadJson   | 28.62 μs | 0.129 μs | 0.121 μs | 1.2512 | 0.0610 |  31.11 KB |
