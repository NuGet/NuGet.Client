// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    /// <summary>
    /// Detects when a package uses the deprecated MonoAndroid framework instead of net6.0-android or later.
    /// This warning is gated on .NET 11 SDK (SdkAnalysisLevel >= 11.0.100) and targeting net11.0-android or later.
    /// </summary>
    internal static class MonoAndroidDeprecation
    {
        /// <summary>
        /// Determines whether the MonoAndroid deprecation check should be performed for the given project and target framework.
        /// </summary>
        /// <param name="project">The package spec containing restore metadata.</param>
        /// <param name="framework">The target framework of the current graph.</param>
        /// <returns>True if the deprecation check should be performed.</returns>
        internal static bool ShouldCheck(PackageSpec project, NuGetFramework framework)
        {
            if (project.RestoreMetadata == null)
            {
                return false;
            }

            // Gate on SDK analysis level >= 11.0.100
            if (!SdkAnalysisLevelMinimums.IsEnabled(
                project.RestoreMetadata.SdkAnalysisLevel,
                project.RestoreMetadata.UsingMicrosoftNETSdk,
                SdkAnalysisLevelMinimums.V11_0_100))
            {
                return false;
            }

            // Only check for .NETCoreApp frameworks targeting android with version >= 11.0
            return StringComparer.OrdinalIgnoreCase.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
                && framework.Version.Major >= 11
                && framework.HasPlatform
                && framework.Platform.Equals("android", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks whether the given lock file target library uses the deprecated MonoAndroid framework
        /// by inspecting the paths of compile-time and runtime assemblies.
        /// </summary>
        /// <param name="library">The lock file target library to check.</param>
        /// <returns>True if the library uses MonoAndroid framework assets.</returns>
        internal static bool UsesMonoAndroidFramework(LockFileTargetLibrary library)
        {
            return ContainsMonoAndroidItem(library.CompileTimeAssemblies)
                || ContainsMonoAndroidItem(library.RuntimeAssemblies);
        }

        private static bool ContainsMonoAndroidItem(IList<LockFileItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                string path = items[i].Path;

                // Paths are like "lib/monoandroid10.0/Assembly.dll" or "ref/monoandroid10.0/Assembly.dll"
                // Extract the framework folder segment (between first and second '/').
                int firstSlash = path.IndexOf('/');
                if (firstSlash >= 0)
                {
                    int secondSlash = path.IndexOf('/', firstSlash + 1);
                    if (secondSlash > firstSlash + 1)
                    {
                        var folderName = path.AsSpan(firstSlash + 1, secondSlash - firstSlash - 1);
                        if (folderName.StartsWith("monoandroid".AsSpan(), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
