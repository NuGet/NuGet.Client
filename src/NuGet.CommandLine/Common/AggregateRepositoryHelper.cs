//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace NuGet.Common
//{
//    public static class AggregateRepositoryHelper
//    {
//        public static AggregateRepository CreateAggregateRepositoryFromSources(
//            IPackageRepositoryFactory factory, 
//            Configuration.IPackageSourceProvider sourceProvider, 
//            IEnumerable<string> sources)
//        {
//            AggregateRepository repository;
//            if (sources != null && sources.Any())
//            {

//                var enabledSources = sourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
//                var sourcesToUse = sources.Select(s => ResolveSource(enabledSources, s));




//                var repositories = sources.Select(s => sourceProvider.ResolveSource(s))
//                                             .Select(factory.CreateRepository)
//                                             .ToList();
//                repository = new AggregateRepository(repositories);
//            }
//            else
//            {
//                repository = sourceProvider.CreateAggregateRepository(factory, ignoreFailingRepositories: true);
//            }

//            return repository;
//        }

//        private static IPackageRepository ResolveSource(
//            IPackageRepositoryFactory repositoryFactory,
//            IEnumerable<Configuration.PackageSource> packageSources,
//            string value)
//        {
//            var result = packageSources.Where(source =>
//                    source.Name.Equals(value, StringComparison.CurrentCultureIgnoreCase) || source.Source.Equals(value, StringComparison.OrdinalIgnoreCase))
//                .FirstOrDefault();

//            if ((result != null && result.ProtocolVersion < 3) || (result == null && value.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
//            {
//                return repositoryFactory.CreateRepository(result.Source);
//            }
//            else
//            {
//                return NullPac
//            }

//            return result?.Source ?? value;
//        }
//    }
//}
