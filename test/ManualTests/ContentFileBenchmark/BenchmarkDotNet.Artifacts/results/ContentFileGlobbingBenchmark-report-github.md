```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.6936/23H2/2023Update/SunValley3) (Hyper-V)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.300-preview.0.26217.103
  [Host]     : .NET 8.0.26 (8.0.2626.16921), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 8.0.26 (8.0.2626.16921), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```
| Method                       | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0      | Allocated | Alloc Ratio |
|----------------------------- |---------:|---------:|---------:|------:|--------:|----------:|----------:|------------:|
| Branch_MatchRelativePath     | 75.43 ms | 1.469 ms | 1.749 ms |  1.00 |    0.03 | 5571.4286 | 135.87 MB |        1.00 |
| Dev_ExecuteWithFileProvider  | 42.66 ms | 0.296 ms | 0.263 ms |  0.57 |    0.01 | 3500.0000 |  84.21 MB |        0.62 |
| Proposed_SingleFileDirectory | 33.74 ms | 0.285 ms | 0.266 ms |  0.45 |    0.01 | 2666.6667 |   64.2 MB |        0.47 |
