// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.SolutionRestoreManager.Test
{
    /// <summary>
    /// Helper class providing a method of building <see cref="IVsProjectRestoreInfo"/>
    /// out of <see cref="PackageSpec"/>.
    /// </summary>
    internal static class ProjectRestoreInfoBuilder
    {
        /// <summary>
        /// Creates project restore info object to be consumed by <see cref="IVsSolutionRestoreService"/>.
        /// </summary>
        /// <param name="packageSpec">Source project restore object</param>
        /// <returns>Desired project restore object</returns>
        public static IVsProjectRestoreInfo Build(PackageSpec packageSpec, string baseIntermediatePath)
        {
            if (packageSpec == null)
            {
                throw new ArgumentNullException(nameof(packageSpec));
            }

            if (packageSpec.TargetFrameworks == null)
            {
                return null;
            }

            var targetFrameworks = new VsTargetFrameworks(
                packageSpec
                    .TargetFrameworks
                    .Select(ToTargetFrameworkInfo));

            var pri = new VsProjectRestoreInfo(
                baseIntermediatePath,
                targetFrameworks);

            if (packageSpec.Tools != null)
            {
                pri.ToolReferences = new VsReferenceItems(
                    packageSpec.Tools.Select(ToToolReference));
            }

            return pri;
        }

        private static VsTargetFrameworkInfo ToTargetFrameworkInfo(TargetFrameworkInformation tfm)
        {
            var packageReferences = new VsReferenceItems(
                tfm.Dependencies
                    .Where(d => d.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                    .Select(ToPackageReference));

            var projectReferences = new VsReferenceItems(
                tfm.Dependencies
                    .Where(d => d.LibraryRange.TypeConstraint == LibraryDependencyTarget.ExternalProject)
                    .Select(ToProjectReference));

            return new VsTargetFrameworkInfo(
                tfm.FrameworkName.ToString(),
                packageReferences,
                projectReferences);
        }

        private static IVsReferenceItem ToPackageReference(LibraryDependency library)
        {
            var properties = new VsReferenceProperties(
                new[] { new VsReferenceProperty("Version", library.LibraryRange.VersionRange.OriginalString) }
            );
            return new VsReferenceItem(library.Name, properties);
        }

        private static IVsReferenceItem ToProjectReference(LibraryDependency library)
        {
            var properties = new VsReferenceProperties(
                new[] { new VsReferenceProperty("ProjectFileFullPath", library.LibraryRange.Name) }
            );
            return new VsReferenceItem(library.Name, properties);
        }

        private static IVsReferenceItem ToToolReference(ToolDependency library)
        {
            var properties = new VsReferenceProperties(
                new[] { new VsReferenceProperty("Version", library.LibraryRange.VersionRange.OriginalString) }
            );
            return new VsReferenceItem(library.LibraryRange.Name, properties);
        }
    }
}
