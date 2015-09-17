// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public class PreviewResult
    {
        // TODO: hook this up to PM
        public IEnumerable<PackageIdentity> Deleted { get; private set; }

        public IEnumerable<PackageIdentity> Added { get; private set; }

        public IEnumerable<UpdatePreviewResult> Updated { get; private set; }

        public string Name { get; private set; }

        public NuGetProject Target { get; private set; }

        public PreviewResult(
            NuGetProject target,
            IEnumerable<PackageIdentity> added,
            IEnumerable<PackageIdentity> deleted,
            IEnumerable<UpdatePreviewResult> updated)
        {
            string s = null;
            if (target.TryGetMetadata(NuGetProjectMetadataKeys.Name, out s))
            {
                Name = s;
            }
            else
            {
                Name = "Unknown Project";
            }

            Target = target;
            Added = added;
            Deleted = deleted;
            Updated = updated;
        }
    }
}
