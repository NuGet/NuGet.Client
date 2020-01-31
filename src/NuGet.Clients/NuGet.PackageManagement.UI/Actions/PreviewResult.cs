// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public class PreviewResult
    {
        public IEnumerable<PackageIdentityResult> Deleted { get; }

        public IEnumerable<PackageIdentityResult> Added { get; }

        public IEnumerable<UpdatePreviewResult> Updated { get; }

        public string Name { get; }

        public NuGetProject Target { get; }

        public PreviewResult(
            NuGetProject target,
            IEnumerable<PackageIdentityResult> added,
            IEnumerable<PackageIdentityResult> deleted,
            IEnumerable<UpdatePreviewResult> updated)
        {
            string s = null;
            if (target.TryGetMetadata(NuGetProjectMetadataKeys.UniqueName, out s))
            {
                Name = s;
            }
            else
            {
                Name = Resources.Preview_UnknownProject;
            }

            Target = target;
            Added = added;
            Deleted = deleted;
            Updated = updated;
        }
    }
}
