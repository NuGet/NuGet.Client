// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet.LibraryModel
{
    public class ToolDependency
    {
        public LibraryRange LibraryRange { get; set; }
        public List<NuGetFramework> Imports { get; set; }
    }
}