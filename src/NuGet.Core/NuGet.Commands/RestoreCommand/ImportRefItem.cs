// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    internal class ImportRefItem
    {
        public LibraryDependency Ref { get; set; }
        public LibraryDependencyIndex DependencyIndex { get; set; } = LibraryDependencyIndex.Invalid;
        public LibraryRangeIndex RangeIndex { get; set; } = LibraryRangeIndex.Invalid;
        public LibraryRangeIndex[] PathToRef { get; set; } = Array.Empty<LibraryRangeIndex>();
        public HashSet<LibraryDependencyIndex> Suppressions { get; set; }
        public IReadOnlyDictionary<LibraryDependencyIndex, VersionRange> CurrentOverrides { get; set; }
        public bool DirectPackageReferenceFromRootProject { get; set; }
    }
}
