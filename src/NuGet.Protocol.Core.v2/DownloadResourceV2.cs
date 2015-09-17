// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    public class DownloadResourceV2 : DownloadResource
    {
        private readonly IPackageRepository V2Client;

        public DownloadResourceV2(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public DownloadResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public override Task<DownloadResourceResult> GetDownloadResourceResultAsync(PackageIdentity identity,
            Configuration.ISettings settings,
            CancellationToken token)
        {
            // settings are not used here, since, global packages folder are not used for v2 sources

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return Task.Run(() =>
            {
                var version = SemanticVersion.Parse(identity.Version.ToString());
                try
                {
                    var package = V2Client.FindPackage(identity.Id, version);

                    if (package != null)
                    {
                        if (V2Client is UnzippedPackageRepository)
                        {
                            var packagePath = Path.Combine(V2Client.Source, identity.Id + "." + version);
                            var directoryInfo = new DirectoryInfo(packagePath);
                            if (directoryInfo.Exists)
                            {
                                return new DownloadResourceResult(package.GetStream(), new PackageFolderReader(directoryInfo));
                            }
                        }

                        return new DownloadResourceResult(package.GetStream());
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    throw new NuGetProtocolException(Strings.FormatProtocol_FailedToDownloadPackage(identity, V2Client.Source), ex);
                }
            });
        }
    }
}
