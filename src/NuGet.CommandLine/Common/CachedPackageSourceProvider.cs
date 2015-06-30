//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace NuGet.Common
//{
//    public class CachedPackageSourceProvider : IPackageSourceProvider
//    {
//        private readonly List<PackageSource> _packageSources;

//        public CachedPackageSourceProvider(IPackageSourceProvider sourceProvider)
//        {
//            if (sourceProvider == null)
//            {
//                throw new ArgumentNullException("sourceProvider");
//            }
//            _packageSources = sourceProvider.LoadPackageSources().ToList();
//        }

//        public IEnumerable<PackageSource> LoadPackageSources()
//        {
//            return _packageSources;
//        }

//        public void SavePackageSources(IEnumerable<PackageSource> sources)
//        {
//            throw new NotSupportedException();
//        }

//        public void DisablePackageSource(PackageSource source)
//        {
//            throw new NotSupportedException();
//        }

//        public bool IsPackageSourceEnabled(PackageSource source)
//        {
//            return source.IsEnabled;
//        }
//    }
//}
