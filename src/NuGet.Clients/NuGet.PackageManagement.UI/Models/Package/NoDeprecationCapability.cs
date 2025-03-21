// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Model;

namespace NuGet.PackageManagement.UI
{
    internal class NoDeprecationCapability : IDeprecationCapable
    {

        public NoDeprecationCapability()
        {
        }

        public bool IsDeprecated => false;

        public PackageDeprecationReasonEnum PackageDeprecationReasons => PackageDeprecationReasonEnum.Unknown;

        public Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
