using System;
using BenchmarkDotNet.Running;

namespace Nuget.Protocol.Benchmarking
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<PerformanceJsonParseTests>();
        }
    }
}
