using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using NuGet.Client;
using NuGet.Commands;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        public RuntimeGraph _runtimeGraph;
        public NuGetv3LocalRepository _repository;
        public NuGetFramework _framework = NuGetFramework.Parse("net9.0");
        public ManagedCodeConventions _managedCodeConventions;
        public LockFileBuilderCache _cache = new();
        public List<(List<SelectionCriteria>, bool)> _orderedCriteria;

        private List<(LocalPackageInfo, NuspecReader)> _packages = new();
        private (LocalPackageInfo, NuspecReader) _microsoftBuildRuntime;
        private (LocalPackageInfo, NuspecReader) _newtonsoftJson;

        public Benchmarks()
        {
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            // Not package specific.
            _runtimeGraph = new RuntimeGraph();
            string path = "E:\\.packages";
            _repository = new NuGetv3LocalRepository(path);
            _managedCodeConventions = new ManagedCodeConventions(_runtimeGraph);
            _cache = new();
            _orderedCriteria = LockFileUtils.CreateOrderedCriteriaSets(_managedCodeConventions, _framework, runtimeIdentifier: null);

            // These can be more than 1 package, for example, they can be a list.

            IEnumerable<NuGet.Protocol.LocalPackageInfo> results = NuGet.Protocol.LocalFolderUtility.GetPackagesV3(path, NuGet.Common.NullLogger.Instance);

            var nj = _repository.FindPackage("Newtonsoft.Json", NuGetVersion.Parse("13.0.3"));
            _newtonsoftJson = (nj, nj.Nuspec);

            var microsoftBuildRuntime = _repository.FindPackage("microsoft.build.runtime", NuGetVersion.Parse("15.1.546"));
            _microsoftBuildRuntime = (microsoftBuildRuntime, microsoftBuildRuntime.Nuspec);

            foreach (NuGet.Protocol.LocalPackageInfo package in results)
            {
                LocalPackageInfo realLocalPackageInfo = _repository.FindPackage(package.Identity.Id, package.Identity.Version);
                _packages.Add((realLocalPackageInfo, realLocalPackageInfo.Nuspec));
            }
        }

        [Benchmark]
        public void NJ()
        {
            var contentItems = _cache.GetContentItems(null, _newtonsoftJson.Item1);

            var library = LockFileBuilder.CreateLockFileLibrary(_newtonsoftJson.Item1, _newtonsoftJson.Item1.Sha512, _newtonsoftJson.Item1.ExpandedPath);
            var lockFileLib = new LockFileTargetLibrary()
            {
                Name = _newtonsoftJson.Item1.Id,
                Version = _newtonsoftJson.Item1.Version,
                Type = LibraryType.Package,
                PackageType = new List<PackageType>()
            };

            LockFileUtils.AddAssets(
                null,
                library,
                _newtonsoftJson.Item1,
                _managedCodeConventions,
                LibraryIncludeFlags.All,
                lockFileLib,
                _framework,
                null,
                contentItems,
                _newtonsoftJson.Item2,
                _orderedCriteria[0].Item1
                );
        }

        [Benchmark]
        public void MicrosoftBuildRuntime()
        {
            var contentItems = _cache.GetContentItems(null, _microsoftBuildRuntime.Item1);

            var library = LockFileBuilder.CreateLockFileLibrary(_microsoftBuildRuntime.Item1, _microsoftBuildRuntime.Item1.Sha512, _microsoftBuildRuntime.Item1.ExpandedPath);
            var lockFileLib = new LockFileTargetLibrary()
            {
                Name = _microsoftBuildRuntime.Item1.Id,
                Version = _microsoftBuildRuntime.Item1.Version,
                Type = LibraryType.Package,
                PackageType = new List<PackageType>()
            };

            LockFileUtils.AddAssets(
                null,
                library,
                _microsoftBuildRuntime.Item1,
                _managedCodeConventions,
                LibraryIncludeFlags.All,
                lockFileLib,
                _framework,
                null,
                contentItems,
               _microsoftBuildRuntime.Item2,
                _orderedCriteria[0].Item1
                );
        }

        [Benchmark]
        public void AllPackages()
        {
            foreach (var package in _packages)
            {
                var contentItems = _cache.GetContentItems(null, package.Item1);

                var library = LockFileBuilder.CreateLockFileLibrary(package.Item1, package.Item1.Sha512, package.Item1.ExpandedPath);
                var lockFileLib = new LockFileTargetLibrary()
                {
                    Name = package.Item1.Id,
                    Version = package.Item1.Version,
                    Type = LibraryType.Package,
                    PackageType = new List<PackageType>()
                };

                LockFileUtils.AddAssets(
                    null,
                    library,
                    package.Item1,
                    _managedCodeConventions,
                    LibraryIncludeFlags.All,
                    lockFileLib,
                    _framework,
                    null,
                    contentItems,
                    package.Item2,
                    _orderedCriteria[0].Item1
                    );

            }
        }
    }


    public class Program
    {

        public static void Main(string[] args)
        {
#if DEBUG
            var benchmark = new Benchmarks();
            benchmark.GlobalSetup();
            benchmark.MicrosoftBuildRuntime();
#else
            var summary = BenchmarkRunner.Run<Benchmarks>();
#endif
        }
    }
}
