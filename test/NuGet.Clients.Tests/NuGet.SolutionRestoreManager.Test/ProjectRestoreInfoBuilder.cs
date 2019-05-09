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
        private readonly VsProjectRestoreInfo _projectRestoreInfo;
        private readonly VsProjectRestoreInfo2 _projectRestoreInfo2;

        private ProjectRestoreInfoBuilder(VsProjectRestoreInfo pri, VsProjectRestoreInfo2 pri2)
        {
            _projectRestoreInfo = pri;
            _projectRestoreInfo2 = pri2;
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

            var targetFrameworks2 = new VsTargetFrameworks2(
                packageSpec
                    .TargetFrameworks
                    .Select(tfm => ToTargetFrameworkInfo2(tfm, projectProperties)));

            var pri = new VsProjectRestoreInfo(
                baseIntermediatePath,
                targetFrameworks);

            var pri2 = new VsProjectRestoreInfo2(
                baseIntermediatePath,
                targetFrameworks2);

            if (crossTargeting)
            {
                pri.OriginalTargetFrameworks = string.Join(";",
                    packageSpec
                        .TargetFrameworks
                        .Select(tfm => tfm.FrameworkName.GetShortFolderName()));
            }

            return new ProjectRestoreInfoBuilder(pri, pri2);
        }

        public ProjectRestoreInfoBuilder WithTool(string name, string version)
        {
            var properties = new VsReferenceProperties
            {
                { "Version", version }
            };

            _projectRestoreInfo.ToolReferences = new VsReferenceItems
            {
                new VsReferenceItem(name, properties)
            };

            _projectRestoreInfo2.ToolReferences = new VsReferenceItems
            {
                new VsReferenceItem(name, properties)
            };

            return this;
        }

        public ProjectRestoreInfoBuilder WithTargetFrameworkInfo(
            IVsTargetFrameworkInfo tfi)
        {
            if(_projectRestoreInfo.TargetFrameworks is VsTargetFrameworks vsTargetFrameworks && tfi is VsTargetFrameworkInfo)
            {
                vsTargetFrameworks.Add(tfi);
            }

            if (_projectRestoreInfo2.TargetFrameworks is VsTargetFrameworks2 vsTargetFrameworks2 && tfi is VsTargetFrameworkInfo2)
            {
                vsTargetFrameworks2.Add((IVsTargetFrameworkInfo2) tfi);
            }

            return this;
        }

        public VsProjectRestoreInfo ProjectRestoreInfo => _projectRestoreInfo;

        public VsProjectRestoreInfo2 ProjectRestoreInfo2 => _projectRestoreInfo2;

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

        private static VsTargetFrameworkInfo2 ToTargetFrameworkInfo2(
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

            var packageDownloads = tfm
                .DownloadDependencies
                .Select(ToPackageDownload);

            var frameworkReferences = tfm.FrameworkReferences.Select(ToFrameworkReference);

            var projectProperties = new VsProjectProperties
            {
                {
                    "PackageTargetFallback",
                    string.Join(";", tfm.Imports.Select(x => x.GetShortFolderName()))
                }
            };

            return new VsTargetFrameworkInfo2(
                tfm.FrameworkName.ToString(),
                packageReferences,
                projectReferences,
                packageDownloads,
                frameworkReferences,
                projectProperties.Concat(globalProperties));
        }

        private static IVsReferenceItem ToFrameworkReference(FrameworkDependency frameworkDependency)
        {
            var properties = new VsReferenceProperties(
                new[] { new VsReferenceProperty("PrivateAssets", FrameworkDependencyFlagsUtils.GetFlagString(frameworkDependency.PrivateAssets)) }
            );
            return new VsReferenceItem(frameworkDependency.Name, properties);
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

        private static IVsReferenceItem ToPackageDownload(DownloadDependency library)
        {
            var properties = new VsReferenceProperties(
                new[] { new VsReferenceProperty("Version", library.VersionRange.OriginalString) }
            );
            return new VsReferenceItem(library.Name, properties);
        }
    }
}
