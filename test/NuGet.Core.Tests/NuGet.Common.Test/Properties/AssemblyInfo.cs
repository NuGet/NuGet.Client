using Xunit;

// XUnit runner configuration: Disable parallel tests
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true, MaxParallelThreads = 1)]
