// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.SolutionRestoreManager.Test
{
    internal class VsTargetFrameworkInfo : IVsTargetFrameworkInfo
    {
        public IVsReferenceItems PackageReferences { get; }

        public IVsReferenceItems ProjectReferences { get; }

        public IVsProjectProperties Properties { get; }

        public String TargetFrameworkMoniker { get; }

        public VsTargetFrameworkInfo(
            string targetFrameworkMoniker,
            IVsReferenceItems packageReferences,
            IVsReferenceItems projectReferences,
            IVsProjectProperties projectProperties)
        {
            if (string.IsNullOrEmpty(targetFrameworkMoniker))
            {
                throw new ArgumentException(ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(targetFrameworkMoniker));
            }

            if (packageReferences == null)
            {
                throw new ArgumentNullException(nameof(packageReferences));
            }

            if (projectReferences == null)
            {
                throw new ArgumentNullException(nameof(projectReferences));
            }

            if (projectProperties == null)
            {
                throw new ArgumentNullException(nameof(projectProperties));
            }

            TargetFrameworkMoniker = targetFrameworkMoniker;
            PackageReferences = packageReferences;
            ProjectReferences = projectReferences;
            Properties = projectProperties;
        }
    }
}
