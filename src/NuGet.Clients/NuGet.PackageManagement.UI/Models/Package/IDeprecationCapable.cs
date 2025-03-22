// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Model;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal interface IDeprecationCapable
    {
        public bool IsDeprecated { get; }

        public PackageDeprecationReasonEnum PackageDeprecationReasons { get; }

        public AlternatePackageMetadataContextInfo? AlternatePackage { get; }

        public Task PopulateDataAsync(CancellationToken cancellationToken);
    }
}
