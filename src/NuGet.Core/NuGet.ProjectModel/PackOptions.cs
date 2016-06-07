// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace NuGet.ProjectModel
{
    public class PackOptions
    {
        public IReadOnlyList<PackageType> PackageType { get; set; }
        public IncludeExcludeFiles IncludeExcludeFiles { get; set; }
        public Dictionary<string, IncludeExcludeFiles> Mappings { get; set; } = new Dictionary<string, IncludeExcludeFiles>();
    }
}
