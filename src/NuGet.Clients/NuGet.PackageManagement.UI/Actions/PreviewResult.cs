// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace NuGet.PackageManagement.UI
{
    public class PreviewResult
    {
        public IEnumerable<AccessiblePackageIdentity> Deleted { get; }

        public IEnumerable<AccessiblePackageIdentity> Added { get; }

        public IEnumerable<UpdatePreviewResult> Updated { get; }

        public IReadOnlyDictionary<string, SortedSet<string>>? NewSourceMappings { get; }

        public string? Name { get; }

        public PreviewResult(
            string? projectName,
            IEnumerable<AccessiblePackageIdentity> added,
            IEnumerable<AccessiblePackageIdentity> deleted,
            IEnumerable<UpdatePreviewResult> updated)
        {
            Name = projectName;
            Added = added;
            Deleted = deleted;
            Updated = updated;
        }

        public PreviewResult(Dictionary<string, SortedSet<string>>? newSourceMappings)
        {
            Name = "Solution";
            NewSourceMappings = newSourceMappings;
            Added = Enumerable.Empty<AccessiblePackageIdentity>();
            Deleted = Enumerable.Empty<AccessiblePackageIdentity>();
            Updated = Enumerable.Empty<UpdatePreviewResult>();
        }
    }
}
