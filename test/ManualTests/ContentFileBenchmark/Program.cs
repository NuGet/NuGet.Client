using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using NuGet.Client;
using NuGet.Commands;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

BenchmarkRunner.Run<ContentFileGlobbingBenchmark>();

/// <summary>
/// Benchmarks the content file globbing hot path: three approaches.
///
/// Dev: Creates SingleFileProvider + FileProviderGlobbingDirectory per file, calls matcher.Execute()
/// Branch: Calls matcher.Match(relativePath) directly
/// Proposed: Uses SingleFileDirectory (1 lightweight class) + matcher.Execute()
///
/// Scans every package in the global packages folder that has contentFiles nuspec entries,
/// and benchmarks the pattern matching loop across 5 frameworks.
/// </summary>
[MemoryDiagnoser]
public class ContentFileGlobbingBenchmark
{
    private static readonly NuGetFramework[] Frameworks = new[]
    {
        NuGetFramework.Parse("net10.0"),
        NuGetFramework.Parse("net8.0"),
        NuGetFramework.Parse("net5.0"),
        NuGetFramework.Parse("net472"),
        NuGetFramework.Parse("netstandard2.0"),
    };

    private const string ContentFilesFolderName = "contentFiles/";

    /// <summary>
    /// Pre-loaded data: for each package+framework, the nuspec entries and the content file relative paths
    /// (already stripped of the "contentFiles/" prefix, just like GetContentFileGroup does).
    /// </summary>
    private List<PackageData> _packages = new();

    [GlobalSetup]
    public void Setup()
    {
        var globalPackagesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        if (!Directory.Exists(globalPackagesFolder))
            throw new DirectoryNotFoundException($"Global packages folder not found: {globalPackagesFolder}");

        var conventions = new ManagedCodeConventions(null);
        int scanned = 0;

        foreach (var packageDir in Directory.EnumerateDirectories(globalPackagesFolder))
        {
            foreach (var versionDir in Directory.EnumerateDirectories(packageDir))
            {
                var nuspecFiles = Directory.GetFiles(versionDir, "*.nuspec");
                if (nuspecFiles.Length == 0) continue;
                scanned++;

                try
                {
                    using var stream = File.OpenRead(nuspecFiles[0]);
                    var nuspec = new NuspecReader(stream);
                    var contentFilesEntries = nuspec.GetContentFiles().ToList();
                    if (contentFilesEntries.Count == 0) continue;

                    // Build file list for this package
                    var prefix = versionDir + Path.DirectorySeparatorChar;
                    var allFiles = Directory.EnumerateFiles(versionDir, "*", SearchOption.AllDirectories)
                        .Select(f => f.Substring(prefix.Length).Replace('\\', '/'))
                        .ToList();

                    var contentItems = new ContentItemCollection();
                    contentItems.Load(allFiles);

                    foreach (var framework in Frameworks)
                    {
                        List<ContentItemGroup> contentFileGroups = new();
                        contentItems.PopulateItemGroups(conventions.Patterns.ContentFiles, contentFileGroups);
                        if (contentFileGroups.Count == 0) continue;

                        // Extract relative paths (stripped of "contentFiles/" prefix)
                        var relativePaths = new List<string>();
                        foreach (var group in contentFileGroups)
                        {
                            foreach (var item in group.Items)
                            {
                                if (item.Path.StartsWith(ContentFilesFolderName, StringComparison.OrdinalIgnoreCase)
                                    && item.Path.Length > ContentFilesFolderName.Length)
                                {
                                    relativePaths.Add(item.Path.Substring(ContentFilesFolderName.Length));
                                }
                            }
                        }

                        if (relativePaths.Count > 0)
                        {
                            _packages.Add(new PackageData
                            {
                                NuspecEntries = contentFilesEntries,
                                RelativePaths = relativePaths,
                            });
                        }
                    }
                }
                catch { }
            }
        }

        int totalOps = _packages.Sum(p => p.RelativePaths.Count * p.NuspecEntries.Count);
        Console.WriteLine($"Scanned {scanned} packages");
        Console.WriteLine($"Loaded {_packages.Count} package/framework combinations with content files");
        Console.WriteLine($"Total match operations per iteration: {totalOps}");
    }

    /// <summary>
    /// BRANCH (after): matcher.Match(relativePath)
    /// </summary>
    [Benchmark(Baseline = true)]
    public int Branch_MatchRelativePath()
    {
        int matches = 0;

        foreach (var pkg in _packages)
        {
            foreach (var entry in pkg.NuspecEntries)
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(entry.Include);
                if (entry.Exclude != null)
                    matcher.AddExclude(entry.Exclude);

                foreach (var relativePath in pkg.RelativePaths)
                {
                    if (matcher.Match(relativePath).HasMatches)
                        matches++;
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// DEV (before): SingleFileProvider + FileProviderGlobbingDirectory + matcher.Execute()
    /// This is the exact code from ContentFileUtils on dev.
    /// </summary>
    [Benchmark]
    public int Dev_ExecuteWithFileProvider()
    {
        int matches = 0;
        var rootDirectory = new VirtualFileInfo(SingleFileProvider.RootDir, isDirectory: true);

        foreach (var pkg in _packages)
        {
            foreach (var entry in pkg.NuspecEntries)
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(entry.Include);
                if (entry.Exclude != null)
                    matcher.AddExclude(entry.Exclude);

                foreach (var relativePath in pkg.RelativePaths)
                {
                    var globbingDirectory = new FileProviderGlobbingDirectory(
                        fileProvider: new SingleFileProvider(relativePath),
                        fileInfo: rootDirectory,
                        parent: null);

                    var matchResults = matcher.Execute(globbingDirectory);
                    if (matchResults.HasMatches)
                        matches++;
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// PROPOSED: SingleFileDirectory (1 lightweight class) + matcher.Execute()
    /// Same Execute approach as dev but with a single minimal class instead of 4 classes + IFileProvider.
    /// No filesystem calls (no GetCurrentDirectory, no GetFullPath).
    /// </summary>
    [Benchmark]
    public int Proposed_Split()
    {
        int matches = 0;

        foreach (var pkg in _packages)
        {
            foreach (var entry in pkg.NuspecEntries)
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(entry.Include);
                if (entry.Exclude != null)
                    matcher.AddExclude(entry.Exclude);

                foreach (var relativePath in pkg.RelativePaths)
                {
                    var dir = new SingleFileDirectory(relativePath);
                    if (matcher.Execute(dir).HasMatches)
                        matches++;
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// REVIEWER SUGGESTION: matcher.Match("ROOT", relativePath)
    /// Uses a virtual root name so Matcher doesn't call Directory.GetCurrentDirectory().
    /// </summary>
    [Benchmark]
    public int MatchRoot_RelativePath()
    {
        int matches = 0;

        foreach (var pkg in _packages)
        {
            foreach (var entry in pkg.NuspecEntries)
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(entry.Include);
                if (entry.Exclude != null)
                    matcher.AddExclude(entry.Exclude);

                foreach (var relativePath in pkg.RelativePaths)
                {
                    if (matcher.Match("ROOT", relativePath).HasMatches)
                        matches++;
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// PROPOSED v2: Span-optimized SingleFileDirectory — avoids Split allocation,
    /// uses index offsets into the original string, Span comparison for GetDirectory.
    /// </summary>
    [Benchmark]
    public int Proposed_Span()
    {
        int matches = 0;

        foreach (var pkg in _packages)
        {
            foreach (var entry in pkg.NuspecEntries)
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(entry.Include);
                if (entry.Exclude != null)
                    matcher.AddExclude(entry.Exclude);

                foreach (var relativePath in pkg.RelativePaths)
                {
                    var dir = new SpanFileDirectory(relativePath);
                    if (matcher.Execute(dir).HasMatches)
                        matches++;
                }
            }
        }

        return matches;
    }
}

public class PackageData
{
    public List<ContentFilesEntry> NuspecEntries { get; set; } = new();
    public List<string> RelativePaths { get; set; } = new();
}

/// <summary>
/// A minimal DirectoryInfoBase that presents a single relative path as a virtual directory tree.
/// Splits the path into segments and lazily creates child directories/files on demand.
/// Zero filesystem calls, minimal allocations.
/// </summary>
public class SingleFileDirectory : DirectoryInfoBase
{
    private readonly string[] _segments;
    private readonly int _index; // -1 = root, 0..n-2 = directory segments, n-1 = file

    public SingleFileDirectory(string relativePath)
        : this(relativePath.Split('/'), -1) { }

    private SingleFileDirectory(string[] segments, int index)
    {
        _segments = segments;
        _index = index;
    }

    public override string Name => _index < 0 ? "." : _segments[_index];
    public override string FullName => _index < 0 ? "/" : string.Join("/", _segments, 0, _index + 1);
    public override DirectoryInfoBase ParentDirectory =>
        _index > 0 ? new SingleFileDirectory(_segments, _index - 1) : this;

    public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
    {
        int childIndex = _index + 1;
        if (childIndex < _segments.Length - 1)
            yield return new SingleFileDirectory(_segments, childIndex);
        else if (childIndex == _segments.Length - 1)
            yield return new SingleFileInfo(_segments, this);
    }

    public override DirectoryInfoBase GetDirectory(string path)
    {
        int childIndex = _index + 1;
        if (childIndex < _segments.Length - 1 &&
            string.Equals(_segments[childIndex], path, StringComparison.OrdinalIgnoreCase))
            return new SingleFileDirectory(_segments, childIndex);
        return new EmptyDirectory(path, this);
    }

    public override FileInfoBase GetFile(string path) => null;
}

public class SingleFileInfo : FileInfoBase
{
    private readonly string[] _segments;
    private readonly DirectoryInfoBase _parent;
    public SingleFileInfo(string[] segments, DirectoryInfoBase parent) { _segments = segments; _parent = parent; }
    public override string Name => _segments[_segments.Length - 1];
    public override string FullName => string.Join("/", _segments);
    public override DirectoryInfoBase ParentDirectory => _parent;
}

public class EmptyDirectory : DirectoryInfoBase
{
    private readonly DirectoryInfoBase _parent;
    public EmptyDirectory(string name, DirectoryInfoBase parent) { Name = name; _parent = parent; }
    public override string Name { get; }
    public override string FullName => Name;
    public override DirectoryInfoBase ParentDirectory => _parent;
    public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos() => [];
    public override DirectoryInfoBase GetDirectory(string path) => new EmptyDirectory(path, this);
    public override FileInfoBase GetFile(string path) => null;
}

/// <summary>
/// Span-optimized version: stores original string + segment start offsets.
/// Avoids Split allocation, uses Span for case-insensitive comparison.
/// </summary>
public class SpanFileDirectory : DirectoryInfoBase
{
    private readonly string _path;
    private readonly int[] _segmentStarts;
    private readonly int _index;

    public SpanFileDirectory(string relativePath)
        : this(relativePath, BuildSegmentStarts(relativePath), -1) { }

    private SpanFileDirectory(string path, int[] segmentStarts, int index)
    {
        _path = path;
        _segmentStarts = segmentStarts;
        _index = index;
    }

    public override string Name => _index < 0 ? "." : GetSegment(_index);
    public override string FullName => _index < 0 ? "/" : _path.Substring(0, GetSegmentEnd(_index));
    public override DirectoryInfoBase ParentDirectory =>
        _index > 0 ? new SpanFileDirectory(_path, _segmentStarts, _index - 1) : this;

    public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
    {
        int childIndex = _index + 1;
        if (childIndex < _segmentStarts.Length - 1)
            yield return new SpanFileDirectory(_path, _segmentStarts, childIndex);
        else if (childIndex == _segmentStarts.Length - 1)
            yield return new SpanFileInfo(_path, _segmentStarts, this);
    }

    public override DirectoryInfoBase GetDirectory(string path)
    {
        int childIndex = _index + 1;
        if (childIndex < _segmentStarts.Length - 1 && SegmentEquals(childIndex, path))
            return new SpanFileDirectory(_path, _segmentStarts, childIndex);
        return new EmptyDirectory(path, this);
    }

    public override FileInfoBase GetFile(string path) => null;

    private string GetSegment(int index)
    {
        int start = _segmentStarts[index];
        int length = GetSegmentEnd(index) - start;
        return _path.Substring(start, length);
    }

    private int GetSegmentEnd(int index)
    {
        return index + 1 < _segmentStarts.Length
            ? _segmentStarts[index + 1] - 1
            : _path.Length;
    }

    private bool SegmentEquals(int index, string value)
    {
        int start = _segmentStarts[index];
        int length = GetSegmentEnd(index) - start;
        return length == value.Length &&
            _path.AsSpan(start, length).Equals(value.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static int[] BuildSegmentStarts(string path)
    {
        int count = 1;
        for (int i = 0; i < path.Length; i++)
            if (path[i] == '/') count++;

        var starts = new int[count];
        starts[0] = 0;
        int seg = 1;
        for (int i = 0; i < path.Length; i++)
            if (path[i] == '/') starts[seg++] = i + 1;

        return starts;
    }
}

public class SpanFileInfo : FileInfoBase
{
    private readonly string _path;
    private readonly int[] _segmentStarts;
    private readonly DirectoryInfoBase _parent;
    public SpanFileInfo(string path, int[] segmentStarts, DirectoryInfoBase parent)
    { _path = path; _segmentStarts = segmentStarts; _parent = parent; }
    public override string Name => _path.Substring(_segmentStarts[_segmentStarts.Length - 1]);
    public override string FullName => _path;
    public override DirectoryInfoBase ParentDirectory => _parent;
}
