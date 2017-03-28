// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.Commands.Rules
{
    public interface IPackageRule
    {
        IEnumerable<PackageIssue> Validate(PackageBuilder builder);
    }
}
