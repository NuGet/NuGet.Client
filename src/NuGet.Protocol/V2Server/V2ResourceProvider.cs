//using System;
//using System.Diagnostics.CodeAnalysis;
//using System.Threading;
//using System.Threading.Tasks;

//namespace NuGet.Client.V2
//{
//    /// <summary>
//    /// Partial implementation for IResourceProvider to do the common V2 specific stuff.
//    /// </summary>
//    public abstract class V2ResourceProvider : ResourceProvider
//    {
//        public abstract Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token);

//        protected async Task<V2Resource> GetRepository(SourceRepository source, CancellationToken token)
//        {
//            var repositoryResource = await source.GetResourceAsync<V2PackageRepositoryResource>(token);

//            if (repositoryResource != null && repositoryResource.V2Client != null)
//            {
//                return repositoryResource;
//            }

//            return null;
//        }
//    }
//}