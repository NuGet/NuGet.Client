// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class PreviewResult
    {
        public IEnumerable<AccessiblePackageIdentity> Deleted { get; }

        public IEnumerable<(AccessiblePackageIdentity accessiblePackageIdentity, VersionRange versionRange)> Added { get; }

        public IEnumerable<UpdatePreviewResult> Updated { get; }

        public string Name { get; }

        public PreviewResult(
            string projectName,
            IEnumerable<(AccessiblePackageIdentity accessiblePackageIdentity, VersionRange versionRange)> added,
            IEnumerable<AccessiblePackageIdentity> deleted,
            IEnumerable<UpdatePreviewResult> updated)
        {
            Name = projectName;
            Added = added;
            Deleted = deleted;
            Updated = updated;
        }
    }
}
