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
    internal class ProjectRestoreInfoBuilder
    {
        private readonly VsProjectRestoreInfo _pri;

        private ProjectRestoreInfoBuilder(VsProjectRestoreInfo pri)
        {
            _pri = pri;
        }

        /// <summary>
        /// Creates project restore info object to be consumed by <see cref="IVsSolutionRestoreService"/>.
        /// </summary>
        /// <param name="packageSpec">Source project restore object</param>
        /// <returns>Desired project restore object</returns>
        public static ProjectRestoreInfoBuilder FromPackageSpec(
            PackageSpec packageSpec,
            string baseIntermediatePath,
            bool crossTargeting)
        {
            if (packageSpec == null)
            {
                throw new ArgumentNullException(nameof(packageSpec));
            }

            if (packageSpec.TargetFrameworks == null)
            {
                return null;
            }

            var projectProperties = new VsProjectProperties { };

            if (packageSpec.Version != null)
            {
                projectProperties = new VsProjectProperties
                {
                    { "PackageVersion", packageSpec.Version.ToString() }
                };
            }

            var targetFrameworks = new VsTargetFrameworks(
                packageSpec
                    .TargetFrameworks
                    .Select(tfm => ToTargetFrameworkInfo(tfm, projectProperties)));

            var pri = new VsProjectRestoreInfo(
                baseIntermediatePath,
                targetFrameworks);

            if (crossTargeting)
            {
                pri.OriginalTargetFrameworks = string.Join(";",
                    packageSpec
                        .TargetFrameworks
                        .Select(tfm => tfm.FrameworkName.GetShortFolderName()));
            }

            return new ProjectRestoreInfoBuilder(pri);
        }

        public ProjectRestoreInfoBuilder WithTool(string name, string version)
        {
            var properties = new VsReferenceProperties
            {
                { "Version", version }
            };

            _pri.ToolReferences = new VsReferenceItems
            {
                new VsReferenceItem(name, properties)
            };

            return this;
        }

        public ProjectRestoreInfoBuilder WithTargetFrameworkInfo(
            IVsTargetFrameworkInfo tfi)
        {
            (_pri.TargetFrameworks as VsTargetFrameworks).Add(tfi);

            return this;
        }

        public VsProjectRestoreInfo Build() => _pri;

        private static VsTargetFrameworkInfo ToTargetFrameworkInfo(
            TargetFrameworkInformation tfm, 
            IEnumerable<IVsProjectProperty> globalProperties)
        {
            var packageReferences = tfm
                .Dependencies
                .Where(d => d.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                .Select(ToPackageReference);

            var projectReferences = tfm
                .Dependencies
                .Where(d => d.LibraryRange.TypeConstraint == LibraryDependencyTarget.ExternalProject)
                .Select(ToProjectReference);

            var projectProperties = new VsProjectProperties
            {
                {
                    "PackageTargetFallback",
                    string.Join(";", tfm.Imports.Select(x => x.GetShortFolderName()))
                }
            };

            return new VsTargetFrameworkInfo(
                tfm.FrameworkName.ToString(),
                packageReferences,
                projectReferences,
                projectProperties.Concat(globalProperties));
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
