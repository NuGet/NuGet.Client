// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        public static VsProjectRestoreInfo Build(
            PackageSpec packageSpec,
            string baseIntermediatePath,
            bool crossTargeting,
            IEnumerable<LibraryRange> tools)
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
                targetFrameworks)
            {
                ToolReferences = new VsReferenceItems(
                    (tools ?? Enumerable.Empty<LibraryRange>()).Select(ToToolReference))
            };

            if (crossTargeting)
            {
                pri.OriginalTargetFrameworks = string.Join(";",
                    packageSpec
                        .TargetFrameworks
                        .Select(tfm => tfm.FrameworkName.GetShortFolderName()));
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

            var projectProperties = new VsProjectProperties(
                new VsProjectProperty(
                    "PackageTargetFallback",
                    string.Join(";", tfm.Imports.Select(x => x.GetShortFolderName())))
            );

            return new VsTargetFrameworkInfo(
                tfm.FrameworkName.ToString(),
                packageReferences,
                projectReferences,
                projectProperties);
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

        private static IVsReferenceItem ToToolReference(LibraryRange libraryRange)
        {
            var properties = new VsReferenceProperties(
                new[] { new VsReferenceProperty("Version", libraryRange.VersionRange.OriginalString) }
            );
            return new VsReferenceItem(libraryRange.Name, properties);
        }
    }
}
