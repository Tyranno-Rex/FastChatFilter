```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
AMD Ryzen 9 5900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK 9.0.101
  [Host]     : .NET 8.0.14 (8.0.1425.11118), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.14 (8.0.1425.11118), X64 RyuJIT AVX2


```
| Method                    | Mean         | Error        | StdDev       | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |-------------:|-------------:|-------------:|------:|--------:|-----:|-------:|----------:|------------:|
| FastChatFilter            | 66,218.12 ns | 1,314.437 ns | 2,966.903 ns | 1.002 |    0.06 |    4 |      - |         - |          NA |
| &#39;AhoCorasick (NReco)&#39;     | 11,827.85 ns |   230.289 ns |   344.685 ns | 0.179 |    0.01 |    3 | 0.5188 |    8800 B |          NA |
| &#39;FastChatFilter (single)&#39; |    365.22 ns |     2.965 ns |     2.774 ns | 0.006 |    0.00 |    2 |      - |         - |          NA |
| &#39;AhoCorasick (single)&#39;    |     73.99 ns |     1.489 ns |     3.451 ns | 0.001 |    0.00 |    1 | 0.0052 |      88 B |          NA |
