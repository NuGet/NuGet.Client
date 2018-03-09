// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class RepositorySignatureResourceProvider : ResourceProvider
    {
        public RepositorySignatureResourceProvider()
           : base(typeof(RepositorySignatureResource),
                 nameof(RepositorySignatureResource),
                 NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            RepositorySignatureResource resource = null;
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);
            if (serviceIndex != null)
            {
                var repoSignUrl = serviceIndex.GetServiceEntryUri(ServiceTypes.RepositorySignatures);

                if (repoSignUrl != null)
                {
                    resource = await GetRepositorySignatureResourceAsync(source, repoSignUrl.AbsoluteUri, NullLogger.Instance, token);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }

        private async Task<RepositorySignatureResource> GetRepositorySignatureResourceAsync(
            SourceRepository source,
            string repoSignUrl,
            ILogger log,
            CancellationToken token)
        {
            var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
            var client = httpSourceResource.HttpSource;

            for (var retry = 0; retry < 3; retry++)
            {
                using (var sourecCacheContext = new SourceCacheContext())
                {
                    var cacheContext = HttpSourceCacheContext.Create(sourecCacheContext, retry);

                    try
                    {
                        return await client.GetAsync(
                            new HttpSourceCachedRequest(
                                repoSignUrl,
                                "repository_signature",
                                cacheContext)
                            {
                                EnsureValidContents = stream => HttpStreamValidation.ValidateJObject(repoSignUrl, stream),
                                MaxTries = 1
                            },
                            async httpSourceResult =>
                            {
                                var json = await httpSourceResult.Stream.AsJObjectAsync();

                                return new RepositorySignatureResource(json, source);
                            },
                            log,
                            token);
                    }
                    catch (Exception ex) when (retry < 2)
                    {
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_RetryingRepositorySignature, repoSignUrl)
                            + Environment.NewLine
                            + ExceptionUtilities.DisplayMessage(ex);
                        log.LogMinimal(message);
                    }
                    catch (Exception ex) when (retry == 2)
                    {
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToReadRepositorySignature, repoSignUrl);

                        throw new FatalProtocolException(message, ex);
                    }
                }
            }

            return null;
        }
    }
}
