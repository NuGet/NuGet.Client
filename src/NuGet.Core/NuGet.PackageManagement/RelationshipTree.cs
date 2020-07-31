// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement
{
    public class RelationshipTree
    {
        public bool IsPopulated => Childs.Any() || Parents.Any() || Descendants.Any() || Ancestors.Any();
        public Dictionary<string, IReadOnlyList<PackageSpec>> Childs { get;} = new Dictionary<string, IReadOnlyList<PackageSpec>>();
        public Dictionary<string, IReadOnlyList<PackageSpec>> Parents { get;} = new Dictionary<string, IReadOnlyList<PackageSpec>>();
        public Dictionary<string, HashSet<PackageSpec>> Descendants { get;} = new Dictionary<string, HashSet<PackageSpec>>();
        public Dictionary<string, HashSet<PackageSpec>> Ancestors { get;} = new Dictionary<string, HashSet<PackageSpec>>();
    }
}
