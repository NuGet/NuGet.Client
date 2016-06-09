// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    public class InstallationCompatibility : IInstallationCompatibility
    {
        private static InstallationCompatibility _instance;

        public static InstallationCompatibility Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new InstallationCompatibility();
                }

                return _instance;
            }
        }

        public void EnsurePackageCompatibility(
            NuGetProject nuGetProject,
            INuGetPathContext pathContext,
            IEnumerable<NuGetProjectAction> nuGetProjectActions,
            RestoreResult restoreResult)
        {
            // Find all of the installed package identities.
            var requestedPackageIds = new HashSet<string>(
                nuGetProjectActions
                    .Where(action => action.NuGetProjectActionType == NuGetProjectActionType.Install)
                    .Select(action => action.PackageIdentity.Id),
                StringComparer.OrdinalIgnoreCase);

            var installedIdentities = restoreResult
                .RestoreGraphs
                .SelectMany(graph => graph.Flattened)
                .Where(result => result.Key.Type == LibraryType.Package)
                .Select(result => new PackageIdentity(result.Key.Name, result.Key.Version))
                .Distinct()
                .Where(identity => requestedPackageIds.Contains(identity.Id));

            // Read the .nuspec on disk and ensure package compatibility.
            var resolver = new FallbackPackagePathResolver(pathContext);
            foreach (var identity in installedIdentities)
            {
                var packageInfo = resolver.GetPackageInfo(
                    identity.Id,
                    identity.Version);

                if (packageInfo != null)
                {
                    var manifestPath = packageInfo.PathResolver.GetManifestFilePath(
                        identity.Id,
                        identity.Version);
                    var nuspecReader = new NuspecReader(manifestPath);

                    EnsurePackageCompatibility(
                        nuGetProject,
                        identity,
                        nuspecReader);
                }
            }
        }

        public void EnsurePackageCompatibility(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            DownloadResourceResult resourceResult)
        {
            NuspecReader nuspecReader;
            if (resourceResult.PackageReader != null)
            {
                nuspecReader = resourceResult.PackageReader.NuspecReader;
            }
            else
            {
                using (var packageReader = new PackageArchiveReader(resourceResult.PackageStream, leaveStreamOpen: true))
                {
                    nuspecReader = packageReader.NuspecReader;
                }
            }

            EnsurePackageCompatibility(
                nuGetProject,
                packageIdentity,
                nuspecReader);
        }

        private static void EnsurePackageCompatibility(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            NuspecReader nuspecReader)
        {
            // Validate that the current version of NuGet satisfies the minVersion attribute specified in the .nuspec
            MinClientVersionUtility.VerifyMinClientVersion(nuspecReader);

            // Validate the package type. There must be zero package types or exactly one package
            // type that is one of the recognized package types.
            var packageTypes = nuspecReader.GetPackageTypes();
            var identityString = $"{packageIdentity.Id} {packageIdentity.Version.ToNormalizedString()}";
            
            if (packageTypes.Count > 1)
            {
                throw new PackagingException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MultiplePackageTypesNotSupported,
                    identityString));
            }
            else if (packageTypes.Count == 1)
            {
                var packageType = packageTypes[0];
                var packageTypeString = packageType.Name;
                if (packageType.Version != PackageType.EmptyVersion)
                {
                    packageTypeString += " " + packageType.Version;
                }

                var projectName = NuGetProject.GetUniqueNameOrName(nuGetProject);

                if (packageType == PackageType.Legacy || // Added for "quirks mode", but not yet fully implemented.
                    packageType == PackageType.Dependency) // A package explicitly stated as a dependency.
                {
                    // These types are always acceptable.
                }
                else if (nuGetProject is ProjectKNuGetProjectBase &&
                         packageType == PackageType.DotnetCliTool)
                {
                    // ProjectKNuGetProjectBase projects are .NET Core (both "dotnet" and "dnx").
                    // .NET CLI tools are support for "dotnet" projects. The projects eventually
                    // call into INuGetPackageManager, which is not implemented by NuGet. This code
                    // will make the decision of how to install the .NET CLI tool package.
                }
                else
                {
                    throw new PackagingException(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UnsupportedPackageType,
                        identityString,
                        packageTypeString,
                        projectName));
                }
            }
        }
    }
}
