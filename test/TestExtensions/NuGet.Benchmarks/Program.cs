using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<LambdaAllocationBenchmarks>();

public class Item
{
    public Item(string value) => Value = value;
    public string Value { get; }
}

[MemoryDiagnoser]
public class LambdaAllocationBenchmarks
{
    [Benchmark]
    public void StaticLambda()
    {
        Create(static path => new Item(path));
    }

    [Benchmark]
    public void Lambda()
    {
        string pre = "my prefix";
        Create(path => new Item(path + pre));
    }

    [Benchmark]
    public void MethodGroup()
    {
        Create(Factory);
    }

    [Benchmark]
    public void DirectCall()
    {
        CreateItem("hello");
    }

    private static Item Factory(string path) => new Item(path);

    private static void Create<T>(Func<string, T> factory)
    {
    }

    private static Item CreateItem(string path)
    {
        return new Item(path);
    }
}
