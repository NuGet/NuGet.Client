using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class Md5VsSha256
    {

        public RuntimeGraph _runtimeGraph;
        public NuGetv3LocalRepository _repository;
        public NuGetFramework _framework = NuGetFramework.Parse("net9.0");
        public ManagedCodeConventions _managedCodeConventions;
        public LockFileBuilderCache _cache = new();
        public List<(List<SelectionCriteria>, bool)> _orderedCriteria;

        //private LocalPackageInfo _package;
        private ContentItemCollection _contentItems;
        private LockFileLibrary _library;
        private LockFileTargetLibrary _lockFileLib;
        //private NuspecReader _nuspecReader;

        private List<(LocalPackageInfo,NuspecReader)> _packages = new();

        public Md5VsSha256()
        {
            //// Not package specific.
            //_runtimeGraph = new RuntimeGraph();
            //_repository = new NuGetv3LocalRepository("E:\\.packages");
            //_managedCodeConventions = new ManagedCodeConventions(_runtimeGraph);
            //_cache = new();
            //_orderedCriteria = LockFileUtils.CreateOrderedCriteriaSets(_managedCodeConventions, _framework, runtimeIdentifier: null);

            //// Are these temp?

            //_package = _repository.FindPackage("NuGet.Commands", NuGetVersion.Parse("6.10.0"));
            //_contentItems = _cache.GetContentItems(null, _package);

            //_library = LockFileBuilder.CreateLockFileLibrary(_package, _package.Sha512, _package.ExpandedPath);
            //_lockFileLib = new LockFileTargetLibrary()
            //{
            //    Name = _package.Id,
            //    Version = _package.Version,
            //    Type = LibraryType.Package,
            //    PackageType = new List<PackageType>()
            //};
            //_nuspecReader = _package.Nuspec;
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
            foreach(NuGet.Protocol.LocalPackageInfo package in results)
            {
                LocalPackageInfo realLocalPackageInfo = _repository.FindPackage(package.Identity.Id, package.Identity.Version);
                _packages.Add((realLocalPackageInfo, realLocalPackageInfo.Nuspec));
            }

            //_package = _repository.FindPackage("newtonsoft.json", NuGetVersion.Parse("13.0.3"));
            //_nuspecReader = _package.Nuspec;
        }

        [Benchmark]
        public void Sha256()
        {
            foreach (var _package in _packages)
            {
                _contentItems = _cache.GetContentItems(null, _package.Item1);

                _library = LockFileBuilder.CreateLockFileLibrary(_package.Item1, _package.Item1.Sha512, _package.Item1.ExpandedPath);
                _lockFileLib = new LockFileTargetLibrary()
                {
                    Name = _package.Item1.Id,
                    Version = _package.Item1.Version,
                    Type = LibraryType.Package,
                    PackageType = new List<PackageType>()
                };

                LockFileUtils.AddAssets(
                    null,
                    _library,
                    _package.Item1,
                    _managedCodeConventions,
                    LibraryIncludeFlags.All,
                    _lockFileLib,
                    _framework,
                    null,
                    _contentItems,
                    _package.Item2,
                    _orderedCriteria[0].Item1
                    );

            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Md5VsSha256>();
        }
    }
}
