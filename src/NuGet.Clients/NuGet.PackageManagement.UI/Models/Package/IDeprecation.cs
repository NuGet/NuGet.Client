// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.Protocol.Model;

namespace NuGet.PackageManagement.UI
{
    interface IDeprecation
    {
        public bool IsDeprecated { get; }

        public PackageDeprecationReasonEnum PackageDeprecationReasons { get; }
    }
}
