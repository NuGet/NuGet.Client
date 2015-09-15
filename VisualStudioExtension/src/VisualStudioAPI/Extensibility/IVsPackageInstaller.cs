// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EnvDTE;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Contains methods to install packages into a project within the current solution.
    /// </summary>
    [ComImport]
    [Guid("4F3B122B-A53B-432C-8D85-0FAFB8BE4FF4")]
    public interface IVsPackageInstaller
    {
        /// <summary>
        /// Installs a single package from the specified package source.
        /// </summary>
        /// <param name="source">The package source to install the package from.</param>
        /// <param name="project">The target project for package installation.</param>
        /// <param name="packageId">The package id of the package to install.</param>
        /// <param name="version">The version of the package to install</param>
        /// <param name="ignoreDependencies">
        /// A boolean indicating whether or not to ignore the package's dependencies
        /// during installation.
        /// </param>
        void InstallPackage(string source, Project project, string packageId, Version version, bool ignoreDependencies);

        /// <summary>
        /// Installs a single package from the specified package source.
        /// </summary>
        /// <param name="source">The package source to install the package from.</param>
        /// <param name="project">The target project for package installation.</param>
        /// <param name="packageId">The package id of the package to install.</param>
        /// <param name="version">The version of the package to install</param>
        /// <param name="ignoreDependencies">
        /// A boolean indicating whether or not to ignore the package's dependencies
        /// during installation.
        /// </param>
        void InstallPackage(string source, Project project, string packageId, string version, bool ignoreDependencies);

        /// <summary>
        /// Installs a single package from the specified package source.
        /// </summary>
        /// <param name="repository">The package repository to install the package from.</param>
        /// <param name="project">The target project for package installation.</param>
        /// <param name="packageId">The package id of the package to install.</param>
        /// <param name="version">The version of the package to install</param>
        /// <param name="ignoreDependencies">
        /// A boolean indicating whether or not to ignore the package's dependencies
        /// during installation.
        /// </param>
        /// <param name="skipAssemblyReferences">
        /// A boolean indicating if assembly references from the package should be
        /// skipped.
        /// </param>
        void InstallPackage(IPackageRepository repository, Project project, string packageId, string version, bool ignoreDependencies, bool skipAssemblyReferences);

        /// <summary>
        /// Installs one or more packages that exist on disk in a folder defined in the registry.
        /// </summary>
        /// <param name="keyName">
        /// The registry key name (under NuGet's repository key) that defines the folder on disk
        /// containing the packages.
        /// </param>
        /// <param name="isPreUnzipped">
        /// A boolean indicating whether the folder contains packages that are
        /// pre-unzipped.
        /// </param>
        /// <param name="skipAssemblyReferences">
        /// A boolean indicating whether the assembly references from the packages
        /// should be skipped.
        /// </param>
        /// <param name="project">The target project for package installation.</param>
        /// <param name="packageVersions">
        /// A dictionary of packages/versions to install where the key is the package id
        /// and the value is the version.
        /// </param>
        /// <remarks>
        /// If any version of the package is already installed, no action will be taken.
        /// <para>
        /// Dependencies are always ignored.
        /// </para>
        /// </remarks>
        void InstallPackagesFromRegistryRepository(string keyName, bool isPreUnzipped, bool skipAssemblyReferences, Project project, IDictionary<string, string> packageVersions);

        /// <summary>
        /// Installs one or more packages that exist on disk in a folder defined in the registry.
        /// </summary>
        /// <param name="keyName">
        /// The registry key name (under NuGet's repository key) that defines the folder on disk
        /// containing the packages.
        /// </param>
        /// <param name="isPreUnzipped">
        /// A boolean indicating whether the folder contains packages that are
        /// pre-unzipped.
        /// </param>
        /// <param name="skipAssemblyReferences">
        /// A boolean indicating whether the assembly references from the packages
        /// should be skipped.
        /// </param>
        /// <param name="ignoreDependencies">A boolean indicating whether the package's dependencies should be ignored</param>
        /// <param name="project">The target project for package installation.</param>
        /// <param name="packageVersions">
        /// A dictionary of packages/versions to install where the key is the package id
        /// and the value is the version.
        /// </param>
        /// <remarks>
        /// If any version of the package is already installed, no action will be taken.
        /// </remarks>
        void InstallPackagesFromRegistryRepository(string keyName, bool isPreUnzipped, bool skipAssemblyReferences, bool ignoreDependencies, Project project, IDictionary<string, string> packageVersions);

        /// <summary>
        /// Installs one or more packages that are embedded in a Visual Studio Extension Package.
        /// </summary>
        /// <param name="extensionId">The Id of the Visual Studio Extension Package.</param>
        /// <param name="isPreUnzipped">
        /// A boolean indicating whether the folder contains packages that are
        /// pre-unzipped.
        /// </param>
        /// <param name="skipAssemblyReferences">
        /// A boolean indicating whether the assembly references from the packages
        /// should be skipped.
        /// </param>
        /// <param name="project">The target project for package installation</param>
        /// <param name="packageVersions">
        /// A dictionary of packages/versions to install where the key is the package id
        /// and the value is the version.
        /// </param>
        /// <remarks>
        /// If any version of the package is already installed, no action will be taken.
        /// <para>
        /// Dependencies are always ignored.
        /// </para>
        /// </remarks>
        void InstallPackagesFromVSExtensionRepository(string extensionId, bool isPreUnzipped, bool skipAssemblyReferences, Project project, IDictionary<string, string> packageVersions);

        /// <summary>
        /// Installs one or more packages that are embedded in a Visual Studio Extension Package.
        /// </summary>
        /// <param name="extensionId">The Id of the Visual Studio Extension Package.</param>
        /// <param name="isPreUnzipped">
        /// A boolean indicating whether the folder contains packages that are
        /// pre-unzipped.
        /// </param>
        /// <param name="skipAssemblyReferences">
        /// A boolean indicating whether the assembly references from the packages
        /// should be skipped.
        /// </param>
        /// <param name="ignoreDependencies">A boolean indicating whether the package's dependencies should be ignored</param>
        /// <param name="project">The target project for package installation</param>
        /// <param name="packageVersions">
        /// A dictionary of packages/versions to install where the key is the package id
        /// and the value is the version.
        /// </param>
        /// <remarks>
        /// If any version of the package is already installed, no action will be taken.
        /// </remarks>
        void InstallPackagesFromVSExtensionRepository(string extensionId, bool isPreUnzipped, bool skipAssemblyReferences, bool ignoreDependencies, Project project, IDictionary<string, string> packageVersions);
    }
}
