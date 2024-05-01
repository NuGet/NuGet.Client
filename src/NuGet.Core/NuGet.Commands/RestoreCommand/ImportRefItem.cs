// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    internal class ImportRefItem
    {
        public LibraryDependency Ref { get; set; }
        public string PathToRef { get; set; }
        public HashSet<string> Suppressions { get; set; }
        public Dictionary<string, VersionRange> CurrentOverrides { get; set; }
    }
}
