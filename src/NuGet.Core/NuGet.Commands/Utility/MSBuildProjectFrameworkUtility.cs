// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet.Commands
{
    public static class MSBuildProjectFrameworkUtility
    {
        /// <summary>
        /// Determine the target framework of an msbuild project.
        /// </summary>
        public static IEnumerable<string> GetProjectFrameworkStrings(
            string projectFilePath,
            string targetFrameworks,
            string targetFramework,
            string targetFrameworkMoniker,
            string targetPlatformIdentifier,
            string targetPlatformVersion,
            string targetPlatformMinVersion)
        {
            return GetProjectFrameworkStrings(
                projectFilePath,
                targetFrameworks,
                targetFramework,
                targetFrameworkMoniker,
                targetPlatformIdentifier,
                targetPlatformVersion,
                targetPlatformMinVersion,
                isManagementPackProject: false,
                isXnaWindowsPhoneProject: false);
        }

        /// <summary>
        /// Determine the target framework of an msbuild project.
        /// </summary>
        public static IEnumerable<string> GetProjectFrameworkStrings(
            string projectFilePath,
            string targetFrameworks,
            string targetFramework,
            string targetFrameworkMoniker,
            string targetPlatformIdentifier,
            string targetPlatformVersion,
            string targetPlatformMinVersion,
            bool isXnaWindowsPhoneProject,
            bool isManagementPackProject)
        {
            var frameworks = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // TargetFrameworks property
            frameworks.UnionWith(MSBuildStringUtility.Split(targetFrameworks));

            if (frameworks.Count > 0)
            {
                return frameworks;
            }

            // TargetFramework property
            var currentFrameworkString = MSBuildStringUtility.TrimAndGetNullForEmpty(targetFramework);

            if (!string.IsNullOrEmpty(currentFrameworkString))
            {
                frameworks.Add(currentFrameworkString);

                return frameworks;
            }

            // C++ check
            if (projectFilePath?.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) == true)
            {
                // The C++ project does not have a TargetFrameworkMoniker property set. 
                // We hard-code the return value to Native.
                frameworks.Add("Native, Version=0.0");

                return frameworks;
            }

            // The MP project does not have a TargetFrameworkMoniker property set. 
            // We hard-code the return value to SCMPInfra.
            if (isManagementPackProject)
            {
                frameworks.Add("SCMPInfra, Version=0.0");

                return frameworks;
            }

            // UAP/Windows store projects
            var platformIdentifier = MSBuildStringUtility.TrimAndGetNullForEmpty(targetPlatformIdentifier);
            var platformVersion = MSBuildStringUtility.TrimAndGetNullForEmpty(targetPlatformMinVersion);

            // if targetPlatformMinVersion isn't defined then fallback to targetPlatformVersion
            if (string.IsNullOrEmpty(platformVersion))
            {
                platformVersion = MSBuildStringUtility.TrimAndGetNullForEmpty(targetPlatformVersion);
            }

            // Check for JS project
            if (projectFilePath?.EndsWith(".jsproj", StringComparison.OrdinalIgnoreCase) == true)
            {
                // JavaScript apps do not have a TargetFrameworkMoniker property set.
                // We read the TargetPlatformIdentifier and targetPlatformMinVersion instead
                // use the default values for JS if they were not given
                if (string.IsNullOrEmpty(platformVersion))
                {
                    platformVersion = "0.0";
                }

                if (string.IsNullOrEmpty(platformIdentifier))
                {
                    platformIdentifier = FrameworkConstants.FrameworkIdentifiers.Windows;
                }

                frameworks.Add($"{platformIdentifier}, Version={platformVersion}");

                return frameworks;
            }

            if (!string.IsNullOrEmpty(platformVersion)
                && StringComparer.OrdinalIgnoreCase.Equals(platformIdentifier, "UAP"))
            {
                // Use the platform id and versions, this is done for UAP projects
                frameworks.Add($"{platformIdentifier}, Version={platformVersion}");

                return frameworks;
            }

            // TargetFrameworkMoniker
            currentFrameworkString = MSBuildStringUtility.TrimAndGetNullForEmpty(targetFrameworkMoniker);

            if (!string.IsNullOrEmpty(currentFrameworkString))
            {
                // XNA project lies about its true identity, reporting itself as a normal .NET 4.0 project.
                // We detect it and changes its target framework to Silverlight4-WindowsPhone71
                if (isXnaWindowsPhoneProject
                    && ".NETFramework,Version=v4.0".Equals(currentFrameworkString, StringComparison.OrdinalIgnoreCase))
                {
                    currentFrameworkString = "Silverlight,Version=v4.0,Profile=WindowsPhone71";
                }

                frameworks.Add(currentFrameworkString);

                return frameworks;
            }

            // Default to unsupported it no framework can be found.
            if (frameworks.Count < 1)
            {
                frameworks.Add(NuGetFramework.UnsupportedFramework.ToString());
            }

            return frameworks;
        }

        /// <summary>
        /// Parse project framework strings into NuGetFrameworks.
        /// </summary>
        public static IEnumerable<NuGetFramework> GetProjectFrameworks(IEnumerable<string> frameworkStrings)
        {
            if (frameworkStrings == null)
            {
                throw new ArgumentNullException(nameof(frameworkStrings));
            }

            var frameworks = new List<NuGetFramework>();

            foreach (var frameworkString in frameworkStrings)
            {
                var parsed = NuGetFramework.Parse(frameworkString);

                // Replace if needed
                parsed = GetProjectFrameworkReplacement(parsed);

                // Add only unique frameworks
                if (!frameworks.Contains(parsed))
                {
                    frameworks.Add(parsed);
                }
            }

            return frameworks;
        }

        /// <summary>
        /// Parse existing nuget framework for .net core 4.5.1 or 4.5 and return compatible framework instance
        /// </summary>
        public static NuGetFramework GetProjectFrameworkReplacement(NuGetFramework framework)
        {
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            // if the framework is .net core 4.5.1 return windows 8.1
            if (framework.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.NetCore)
                && framework.Version.Equals(Version.Parse("4.5.1.0")))
            {
                return new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows,
                       new Version("8.1"), framework.Profile);
            }
            // if the framework is .net core 4.5 return 8.0
            if (framework.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.NetCore)
                && framework.Version.Equals(Version.Parse("4.5.0.0")))
            {
                return new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows,
                       new Version("8.0"), framework.Profile);
            }

            return framework;
        }

        private static string GetPropertyOrNull(string propertyName, IDictionary<string, string> projectProperties)
        {
            string value;
            if (projectProperties.TryGetValue(propertyName, out value)
                && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return null;
        }
    }
}
