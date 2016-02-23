// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.Frameworks;
using NuGet.VisualStudio.Implementation.Resources;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsFrameworkCompatibility))]
    public class VsFrameworkCompatibility : IVsFrameworkCompatibility
    {
        public IEnumerable<FrameworkName> GetNetStandardFrameworks()
        {
            return DefaultFrameworkNameProvider
                .Instance
                .GetNetStandardVersions()
                .Select(GetFrameworkName);
        }

        public IEnumerable<FrameworkName> GetFrameworksSupportingNetStandard(FrameworkName frameworkName)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException(nameof(frameworkName));
            }

            var nuGetFramework = GetNuGetFramework(frameworkName);

            if (!StringComparer.OrdinalIgnoreCase.Equals(
                nuGetFramework.Framework,
                FrameworkConstants.FrameworkIdentifiers.NetStandard))
            {
                throw new ArgumentException(string.Format(
                    VsResources.InvalidNetStandardFramework,
                    frameworkName));
            }

            return CompatibilityListProvider
                .Default
                .GetFrameworksSupporting(nuGetFramework)
                .Select(GetFrameworkName);
        }

        private NuGetFramework GetNuGetFramework(FrameworkName frameworkName)
        {
            return NuGetFramework.ParseFrameworkName(frameworkName.ToString(), DefaultFrameworkNameProvider.Instance);
        }

        private FrameworkName GetFrameworkName(NuGetFramework nuGetFramework)
        {
            return new FrameworkName(nuGetFramework.DotNetFrameworkName);
        }
    }
}
