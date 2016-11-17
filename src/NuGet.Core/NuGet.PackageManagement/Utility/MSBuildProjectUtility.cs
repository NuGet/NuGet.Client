// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.PackageManagement
{
    public static class MSBuildProjectUtility
    {
        public static string GetTargetFrameworkString(dynamic msbuildProject)
        {
            if (msbuildProject == null)
            {
                return null;
            }

            var extension = msbuildProject.GetPropertyValue(ProjectManagement.Constants.ProjectExt);

            // Check for JS project
            if (StringComparer.OrdinalIgnoreCase.Equals(ProjectManagement.Constants.JSProjectExt, extension))
            {
                // JavaScript apps do not have a TargetFrameworkMoniker property set.
                // We read the TargetPlatformIdentifier and TargetPlatformVersion instead
                var platformIdentifier = msbuildProject.GetPropertyValue(ProjectManagement.Constants.TargetPlatformIdentifier);
                var platformVersion = msbuildProject.GetPropertyValue(ProjectManagement.Constants.TargetPlatformVersion);

                // use the default values for JS if they were not given
                if (string.IsNullOrEmpty(platformVersion))
                {
                    platformVersion = "0.0";
                }

                if (string.IsNullOrEmpty(platformIdentifier))
                {
                    platformIdentifier = "Windows";
                }

                return string.Format(CultureInfo.InvariantCulture, "{0}, Version={1}", platformIdentifier, platformVersion);
            }

            // Check for C++ project
            if (StringComparer.OrdinalIgnoreCase.Equals(ProjectManagement.Constants.VCXProjextExt, extension))
            {
                // The C++ project does not have a TargetFrameworkMoniker property set. 
                // We hard-code the return value to Native.
                return ProjectManagement.Constants.NativeTFM;
            }

            return msbuildProject.GetPropertyValue(ProjectManagement.Constants.TargetFrameworkMoniker);
        }
    }
}
