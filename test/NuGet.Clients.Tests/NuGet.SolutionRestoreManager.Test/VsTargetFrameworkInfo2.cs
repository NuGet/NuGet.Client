// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager.Test
{
    internal class VsTargetFrameworkInfo2 : IVsTargetFrameworkInfo2
    {
        public IVsReferenceItems PackageReferences { get; }

        public IVsReferenceItems ProjectReferences { get; }

        public IVsReferenceItems PackageDownloads { get; }

        public IVsProjectProperties Properties { get; }

        public string TargetFrameworkMoniker { get; }

        public VsTargetFrameworkInfo2(
            string targetFrameworkMoniker,
            IEnumerable<IVsReferenceItem> packageReferences,
            IEnumerable<IVsReferenceItem> projectReferences,
            IEnumerable<IVsReferenceItem> packageDownloads,
            IEnumerable<IVsProjectProperty> projectProperties)
        {
            if (string.IsNullOrEmpty(targetFrameworkMoniker))
            {
                throw new ArgumentException("Argument cannot be null or empty", nameof(targetFrameworkMoniker));
            }

            if (packageReferences == null)
            {
                throw new ArgumentNullException(nameof(packageReferences));
            }

            if (projectReferences == null)
            {
                throw new ArgumentNullException(nameof(projectReferences));
            }

            if (packageDownloads == null)
            {
                throw new ArgumentNullException(nameof(packageDownloads));
            }

            if (projectProperties == null)
            {
                throw new ArgumentNullException(nameof(projectProperties));
            }

            TargetFrameworkMoniker = targetFrameworkMoniker;
            PackageReferences = new VsReferenceItems(packageReferences);
            ProjectReferences = new VsReferenceItems(projectReferences);
            PackageDownloads = new VsReferenceItems(packageDownloads);
            Properties = new VsProjectProperties(projectProperties);
        }
    }
}
