//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using NuGet.Protocol.Core.Types;

//namespace NuGet.CommandLine.Common
//{
//    public class V3PackageRepository : IPackageRepository, IPackageLookup
//    {
//        private readonly Lazy<FindPackageByIdResource> _resource;

//        public V3PackageRepository(string source)
//        {
//            Source = source;
//            var repository = Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Factory, source);
//            _resource = new Lazy<FindPackageByIdResource>(() => repository.GetResource<FindPackageByIdResource>());
//        }

//        public PackageSaveModes PackageSaveMode { get; set; }

//        public string Source { get; }

//        public bool SupportsPrereleasePackages => true;

//        public void AddPackage(IPackage package)
//        {
//            throw new NotSupportedException();
//        }

//        public bool Exists(string packageId, SemanticVersion version)
//        {
//            throw new NotImplementedException();
//        }

//        public IPackage FindPackage(string packageId, SemanticVersion version)
//        {
//            var stream = _resource.Value.GetNupkgStreamAsync(
//                packageId,
//                new Versioning.NuGetVersion(version.ToNormalizedString()),
//                CancellationToken.None).Result;
//            return new ZipPackage(stream);
//        }

//        public IEnumerable<IPackage> FindPackagesById(string packageId)
//        {
//            throw new NotImplementedException();
//        }

//        public IQueryable<IPackage> GetPackages()
//        {
//            throw new NotSupportedException();
//        }

//        public void RemovePackage(IPackage package)
//        {
//            throw new NotSupportedException();
//        }
//    }
//}
