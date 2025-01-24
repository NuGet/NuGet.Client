// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    internal interface IListPackageCommandRunner
    {
        Task<int> ExecuteCommandAsync(ListPackageArgs packageRefArgs, IReadOnlyList<PackageSource> auditSources);
    }
}
