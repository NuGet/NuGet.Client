// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Model;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal class NoDeprecationCapability : IDeprecationCapable
    {
        public bool IsDeprecated => false;

        public PackageDeprecationReasonEnum PackageDeprecationReasons => PackageDeprecationReasonEnum.Unknown;

        public AlternatePackageMetadataContextInfo? AlternatePackage => null;

        public Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
