// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Versioning;

using NuGet.Frameworks;

namespace NuGet.Packaging
{
    public static class FrameworksExtensions
    {
        // NuGet.Frameworks doesn't have the equivalent of the old VersionUtility.GetFrameworkString
        // which is relevant for building packages
        public static string GetFrameworkString(this NuGetFramework self)
        {
            var frameworkName = new FrameworkName(self.DotNetFrameworkName);
            string name = frameworkName.Identifier + frameworkName.Version;
            if (string.IsNullOrEmpty(frameworkName.Profile))
            {
                return name;
            }
            return name + "-" + frameworkName.Profile;
        }
    }
}
