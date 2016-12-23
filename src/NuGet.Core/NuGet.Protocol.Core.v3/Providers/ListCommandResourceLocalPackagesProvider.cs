// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class ListCommandResourceLocalPackagesProvider : ResourceProvider
    {
        public ListCommandResourceLocalPackagesProvider()
            : base(
                  typeof(ListCommandResource),
                  nameof(ListCommandResourceLocalPackagesProvider),
                  NuGetResourceProviderPositions.Last) { }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {

           var findLocalPackagesResource =  await source.GetResourceAsync<FindLocalPackagesResource>();

            ListCommandResource listCommandResource = null;
            if (findLocalPackagesResource != null)
            {
                listCommandResource = new ListCommandResource(source.PackageSource.Source);
            }

            var result = new Tuple<bool, INuGetResource>(listCommandResource != null, listCommandResource);
            return result;
        }
    }
}
