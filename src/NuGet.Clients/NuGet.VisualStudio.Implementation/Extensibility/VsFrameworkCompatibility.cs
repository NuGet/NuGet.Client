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
                .Select(framework => new FrameworkName(framework.DotNetFrameworkName));
        }

        public IEnumerable<FrameworkName> GetFrameworksSupportingNetStandard(FrameworkName frameworkName)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException(nameof(frameworkName));
            }

            var nuGetFramework = NuGetFramework.ParseFrameworkName(frameworkName.ToString(), DefaultFrameworkNameProvider.Instance);

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
                .Select(framework => new FrameworkName(framework.DotNetFrameworkName));
        }

        public FrameworkName GetNearest(FrameworkName targetFramework, IEnumerable<FrameworkName> frameworks)
        {
            if (targetFramework == null)
            {
                throw new ArgumentNullException(nameof(targetFramework));
            }

            if (frameworks == null)
            {
                throw new ArgumentNullException(nameof(frameworks));
            }

            var nuGetTargetFramework = NuGetFramework.ParseFrameworkName(targetFramework.ToString(), DefaultFrameworkNameProvider.Instance);
            var nuGetFrameworks = frameworks
                .Select(framework => NuGetFramework.ParseFrameworkName(framework.ToString(), DefaultFrameworkNameProvider.Instance));
            
            var reducer = new FrameworkReducer();
            var nearest = reducer.GetNearest(nuGetTargetFramework, nuGetFrameworks);

            if (nearest == null)
            {
                return null;
            }

            return new FrameworkName(nearest.DotNetFrameworkName);
        }
    }
}
