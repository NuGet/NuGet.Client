using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace BenchmarkTemplate
{
    public class Benchmarks
    {
        public Benchmarks()
        {
        }

        [Benchmark]
        public int example() => 1 + 1;
    }

    class RunBenchmarks
    {
        // Run this using: `dotnet run --configuration Release`
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Benchmarks>();
        }
    }
}
