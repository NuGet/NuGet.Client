﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager.Test
{
    internal class VsTargetFrameworkInfo : IVsTargetFrameworkInfo
    {
        public IVsReferenceItems PackageReferences { get; }

        public IVsReferenceItems ProjectReferences { get; }

        public IVsProjectProperties Properties { get; }

        public string TargetFrameworkMoniker { get; }

        public VsTargetFrameworkInfo(
            string targetFrameworkMoniker,
            IEnumerable<IVsReferenceItem> packageReferences,
            IEnumerable<IVsReferenceItem> projectReferences,
            IEnumerable<IVsProjectProperty> projectProperties)
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
            PackageReferences = new VsReferenceItems(packageReferences);
            ProjectReferences = new VsReferenceItems(projectReferences);
            Properties = new VsProjectProperties(projectProperties);
        }
    }
}
