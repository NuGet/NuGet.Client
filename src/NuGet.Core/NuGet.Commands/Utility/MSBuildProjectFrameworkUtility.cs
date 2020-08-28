// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
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
        /// Given the properties from an msbuild project file and a the file path, infer the target framework.
        /// This method prioritizes projects without a framework, such as vcxproj and accounts for the mismatching arguments in UAP projects, where the TFI and TFV are set but should be ignored.
        /// Likewise, this method will *ignore* unnecessary properties, such as TPI and TPV when profiles are used, and frameworks that do not support platforms have some default values.
        /// </summary>
        /// <returns>The inferred framework. Unsupported otherwise.</returns>
        public static NuGetFramework GetProjectFramework(
            string projectFilePath,
            string targetFrameworkMoniker,
            string targetPlatformMoniker,
            string targetPlatformMinVersion)
        {
            return GetProjectFramework(
                projectFilePath,
                targetFrameworkMoniker,
                targetPlatformMoniker,
                targetPlatformIdentifier: null,
                targetPlatformVersion: null,
                targetPlatformMinVersion,
                isXnaWindowsPhoneProject: false,
                isManagementPackProject: false,
                GetAsNuGetFramework);
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
            return GetProjectFrameworks(
                projectFilePath,
                targetFrameworks,
                targetFramework,
                targetFrameworkMoniker,
                targetPlatformMoniker: null,
                targetPlatformIdentifier,
                targetPlatformVersion,
                targetPlatformMinVersion,
                isXnaWindowsPhoneProject,
                isManagementPackProject,
                GetAsFrameworkString);
        }

        internal static IEnumerable<T> GetProjectFrameworks<T>(
            string projectFilePath,
            string targetFrameworks,
            string targetFramework,
            string targetFrameworkMoniker,
            string targetPlatformMoniker,
            string targetPlatformIdentifier,
            string targetPlatformVersion,
            string targetPlatformMinVersion,
            bool isXnaWindowsPhoneProject,
            bool isManagementPackProject,
            Func<object, T> valueFactory)
        {
            var frameworks = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // TargetFrameworks property
            frameworks.UnionWith(MSBuildStringUtility.Split(targetFrameworks));

            if (frameworks.Count > 0)
            {
                return frameworks.Select(e => valueFactory(e));
            }

            // TargetFramework property
            var currentFrameworkString = MSBuildStringUtility.TrimAndGetNullForEmpty(targetFramework);

            if (!string.IsNullOrEmpty(currentFrameworkString))
            {
                frameworks.Add(currentFrameworkString);

                return frameworks.Select(e => valueFactory(e));
            }

            return new T[] { GetProjectFramework(
                projectFilePath,
                targetFrameworkMoniker,
                targetPlatformMoniker,
                targetPlatformIdentifier,
                targetPlatformVersion,
                targetPlatformMinVersion,
                isXnaWindowsPhoneProject,
                isManagementPackProject,
                valueFactory) };
        }

        internal static T GetProjectFramework<T>(
            string projectFilePath,
            string targetFrameworkMoniker,
            string targetPlatformMoniker,
            string targetPlatformIdentifier,
            string targetPlatformVersion,
            string targetPlatformMinVersion,
            bool isXnaWindowsPhoneProject,
            bool isManagementPackProject,
            Func<object, T> valueFactory)
        {
            // C++ check
            if (projectFilePath?.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) == true)
            {
                // The C++ project does not have a TargetFrameworkMoniker property set. 
                // We hard-code the return value to Native.
                return valueFactory("Native, Version=0.0");
            }

            // The MP project does not have a TargetFrameworkMoniker property set. 
            // We hard-code the return value to SCMPInfra.
            if (isManagementPackProject)
            {
                return valueFactory("SCMPInfra, Version=0.0");
            }

            // UAP/Windows store projects
            var platformMoniker = MSBuildStringUtility.TrimAndGetNullForEmpty(targetPlatformMoniker);
            var platformMonikerIdentifier = GetParts(platformMoniker).FirstOrDefault();

            var platformIdentifier = MSBuildStringUtility.TrimAndGetNullForEmpty(targetPlatformIdentifier);
            var platformMinVersion = MSBuildStringUtility.TrimAndGetNullForEmpty(targetPlatformMinVersion);
            var platformVersion = MSBuildStringUtility.TrimAndGetNullForEmpty(targetPlatformVersion);

            var effectivePlatformVersion = platformMinVersion ?? platformVersion;
            var effectivePlatformIdentifier = platformMonikerIdentifier ?? platformIdentifier;

            // Check for JS project
            if (projectFilePath?.EndsWith(".jsproj", StringComparison.OrdinalIgnoreCase) == true)
            {
                // JavaScript apps do not have a TargetFrameworkMoniker property set.
                // We read the TargetPlatformIdentifier and targetPlatformMinVersion instead
                // use the default values for JS if they were not given

                // Prefer moniker over individual properties
                if (!string.IsNullOrEmpty(platformMoniker))
                {
                    return GetFrameworkFromMoniker(valueFactory, platformMonikerIdentifier, platformMoniker, platformMinVersion);
                }

                if (string.IsNullOrEmpty(effectivePlatformVersion))
                {
                    effectivePlatformVersion = "0.0";
                }

                if (string.IsNullOrEmpty(effectivePlatformIdentifier))
                {
                    effectivePlatformIdentifier = FrameworkConstants.FrameworkIdentifiers.Windows;
                }

                return valueFactory($"{effectivePlatformIdentifier}, Version={effectivePlatformVersion}");
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(effectivePlatformIdentifier, "UAP"))
            {
                // Use the platform id and versions, this is done for UAP projects
                // Prefer moniker over individual properties
                if (!string.IsNullOrEmpty(platformMoniker))
                {
                    return GetFrameworkFromMoniker(valueFactory, effectivePlatformIdentifier, platformMoniker, platformMinVersion);
                }
                if (!string.IsNullOrEmpty(effectivePlatformVersion))
                {
                    return valueFactory($"{effectivePlatformIdentifier}, Version={effectivePlatformVersion}");
                }
            }

            // TargetFrameworkMoniker
            var currentFrameworkString = MSBuildStringUtility.TrimAndGetNullForEmpty(targetFrameworkMoniker);

            if (!string.IsNullOrEmpty(currentFrameworkString))
            {
                // XNA project lies about its true identity, reporting itself as a normal .NET 4.0 project.
                // We detect it and changes its target framework to Silverlight4-WindowsPhone71
                if (isXnaWindowsPhoneProject
                    && ".NETFramework,Version=v4.0".Equals(currentFrameworkString, StringComparison.OrdinalIgnoreCase))
                {
                    currentFrameworkString = "Silverlight,Version=v4.0,Profile=WindowsPhone71";
                    return valueFactory(currentFrameworkString);
                }
                NuGetFramework framework = NuGetFramework.ParseComponents(currentFrameworkString, platformMoniker);

                return valueFactory(framework);
            }

            // Default to unsupported it no framework was found.
            return valueFactory(NuGetFramework.UnsupportedFramework);
        }

        private static T GetFrameworkFromMoniker<T>(Func<object, T> valueFactory, string platformIdentifier, string platformMoniker, string platformMinVersion)
        {
            if (!string.IsNullOrEmpty(platformMinVersion))
            {
                return valueFactory($"{platformIdentifier}, Version={platformMinVersion}");
            }
            else
            {
                return valueFactory(platformMoniker);
            }
        }

        private static string[] GetParts(string targetPlatformMoniker)
        {
            return targetPlatformMoniker != null ?
                targetPlatformMoniker.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray() :
                new string[] { };
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

        /// <summary>
        /// Get a NuGetFramework out of the passed object. The argument is expected to either be a <see cref="NuGetFramework"/> or <see cref="string"/>.
        /// </summary>
        private static NuGetFramework GetAsNuGetFramework(object arg)
        {
            if (arg is NuGetFramework nugetFramework)
            {
                return nugetFramework;
            }
            if (arg is string frameworkString)
            {
                return NuGetFramework.Parse(frameworkString);
            }
            throw new ArgumentException("Unexpected object type");
        }

        /// <summary>
        /// Get a roundtrippable framework string out of the passed object. The argument is expected to either be a <see cref="NuGetFramework"/> or <see cref="string"/>.
        /// </summary>
        private static string GetAsFrameworkString(object arg)
        {
            if (arg is string str)
            {
                return str;
            }
            if (arg is NuGetFramework framework)
            {
                return framework.DotNetFrameworkName;
            }

            throw new ArgumentException("Unexpected object type");
        }
    }
}
