// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Writes out a PackageSpec object graph.
    /// 
    /// This is non-private only to facilitate unit testing.
    /// </summary>
    public sealed class PackageSpecWriter
    {
        /// <summary>
        /// Writes a PackageSpec to an <c>NuGet.Common.IObjectWriter</c> instance. 
        /// </summary>
        /// <param name="packageSpec">A <c>PackageSpec</c> instance.</param>
        /// <param name="writer">An <c>NuGet.Common.IObjectWriter</c> instance.</param>
        public static void Write(PackageSpec packageSpec, IObjectWriter writer)
        {
            if (packageSpec == null)
            {
                throw new ArgumentNullException(nameof(packageSpec));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            SetValue(writer, "title", packageSpec.Title);

            if (!packageSpec.IsDefaultVersion)
            {
                SetValue(writer, "version", packageSpec.Version?.ToFullString());
            }

            SetValue(writer, "description", packageSpec.Description);
            SetArrayValue(writer, "authors", packageSpec.Authors);
            SetValue(writer, "copyright", packageSpec.Copyright);
            SetValue(writer, "language", packageSpec.Language);
            SetArrayValue(writer, "contentFiles", packageSpec.ContentFiles);
            SetDictionaryValue(writer, "packInclude", packageSpec.PackInclude);
            SetPackOptions(writer, packageSpec);
            SetMSBuildMetadata(writer, packageSpec);
            SetDictionaryValues(writer, "scripts", packageSpec.Scripts);

            if (packageSpec.Dependencies.Any())
            {
                SetDependencies(writer, packageSpec.Dependencies);
            }

            SetFrameworks(writer, packageSpec.TargetFrameworks);

            JsonRuntimeFormat.WriteRuntimeGraph(writer, packageSpec.RuntimeGraph);
        }

        /// <summary>
        /// Writes a PackageSpec to a file.
        /// </summary>
        /// <param name="packageSpec">A <c>PackageSpec</c> instance.</param>
        /// <param name="filePath">A file path to write to.</param>
        public static void WriteToFile(PackageSpec packageSpec, string filePath)
        {
            if (packageSpec == null)
            {
                throw new ArgumentNullException(nameof(packageSpec));
            }

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException(Strings.ArgumentNullOrEmpty, nameof(filePath));
            }

            var writer = new RuntimeModel.JsonObjectWriter();

            Write(packageSpec, writer);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            using (var textWriter = new StreamWriter(fileStream))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;
                writer.WriteTo(jsonWriter);
            }
        }

        private static void SetMSBuildMetadata(IObjectWriter writer, PackageSpec packageSpec)
        {
            var msbuildMetadata = packageSpec.RestoreMetadata;
            if (msbuildMetadata == null)
            {
                return;
            }

            if (msbuildMetadata.ProjectUniqueName == null && msbuildMetadata.ProjectName == null
                && msbuildMetadata.ProjectPath == null && msbuildMetadata.ProjectJsonPath == null
                && msbuildMetadata.PackagesPath == null && msbuildMetadata.OutputPath == null)
            {
                return;
            }

            writer.WriteObjectStart(JsonPackageSpecReader.RestoreOptions);

            SetValue(writer, "projectUniqueName", msbuildMetadata.ProjectUniqueName);
            SetValue(writer, "projectName", msbuildMetadata.ProjectName);
            SetValue(writer, "projectPath", msbuildMetadata.ProjectPath);
            SetValue(writer, "projectJsonPath", msbuildMetadata.ProjectJsonPath);
            SetValue(writer, "packagesPath", msbuildMetadata.PackagesPath);
            SetValue(writer, "outputPath", msbuildMetadata.OutputPath);

            if (msbuildMetadata.ProjectStyle != ProjectStyle.Unknown)
            {
                SetValue(writer, "projectStyle", msbuildMetadata.ProjectStyle.ToString());
            }

            if (msbuildMetadata.CrossTargeting)
            {
                SetValue(writer, "crossTargeting", msbuildMetadata.CrossTargeting.ToString());
            }

            if (msbuildMetadata.LegacyPackagesDirectory)
            {
                SetValue(
                    writer,
                    "legacyPackagesDirectory",
                    msbuildMetadata.LegacyPackagesDirectory.ToString());
            }

            SetArrayValue(writer, "fallbackFolders", msbuildMetadata.FallbackFolders);
            SetArrayValue(writer, "originalTargetFrameworks", msbuildMetadata.OriginalTargetFrameworks);

            if (msbuildMetadata.Sources?.Count > 0)
            {
                writer.WriteObjectStart("sources");

                foreach (var source in msbuildMetadata.Sources)
                {
                    // "source": {}
                    writer.WriteObjectStart(source.Source);
                    writer.WriteObjectEnd();
                }

                writer.WriteObjectEnd();
            }

            if (msbuildMetadata.TargetFrameworks?.Count > 0)
            {
                writer.WriteObjectStart("frameworks");

                var frameworkNames = new HashSet<string>();

                foreach (var framework in msbuildMetadata.TargetFrameworks)
                {
                    var frameworkName = framework.FrameworkName.GetShortFolderName();

                    if (!frameworkNames.Contains(frameworkName))
                    {
                        frameworkNames.Add(frameworkName);

                        writer.WriteObjectStart(frameworkName);

                        writer.WriteObjectStart("projectReferences");

                        foreach (var project in framework.ProjectReferences)
                        {
                            writer.WriteObjectStart(project.ProjectUniqueName);

                            writer.WriteNameValue("projectPath", project.ProjectPath);

                            if (project.IncludeAssets != LibraryIncludeFlags.All)
                            {
                                writer.WriteNameValue("includeAssets", LibraryIncludeFlagUtils.GetFlagString(project.IncludeAssets));
                            }

                            if (project.ExcludeAssets != LibraryIncludeFlags.None)
                            {
                                writer.WriteNameValue("excludeAssets", LibraryIncludeFlagUtils.GetFlagString(project.ExcludeAssets));
                            }

                            if (project.PrivateAssets != LibraryIncludeFlagUtils.DefaultSuppressParent)
                            {
                                writer.WriteNameValue("privateAssets", LibraryIncludeFlagUtils.GetFlagString(project.PrivateAssets));
                            }

                            writer.WriteObjectEnd();
                        }

                        writer.WriteObjectEnd();
                        writer.WriteObjectEnd();
                    }
                }

                writer.WriteObjectEnd();
            }

            writer.WriteObjectEnd();
        }

        private static void SetPackOptions(IObjectWriter writer, PackageSpec packageSpec)
        {
            var packOptions = packageSpec.PackOptions;
            if (packOptions == null)
            {
                return;
            }

            if ((packageSpec.Owners == null || packageSpec.Owners.Length == 0)
                && (packageSpec.Tags == null || packageSpec.Tags.Length == 0)
                && packageSpec.ProjectUrl == null && packageSpec.IconUrl == null && packageSpec.Summary == null
                && packageSpec.ReleaseNotes == null && packageSpec.LicenseUrl == null
                && !packageSpec.RequireLicenseAcceptance
                && (packOptions.PackageType == null || packOptions.PackageType.Count == 0))
            {
                return;
            }

            writer.WriteObjectStart(JsonPackageSpecReader.PackOptions);

            SetArrayValue(writer, "owners", packageSpec.Owners);
            SetArrayValue(writer, "tags", packageSpec.Tags);
            SetValue(writer, "projectUrl", packageSpec.ProjectUrl);
            SetValue(writer, "iconUrl", packageSpec.IconUrl);
            SetValue(writer, "summary", packageSpec.Summary);
            SetValue(writer, "releaseNotes", packageSpec.ReleaseNotes);
            SetValue(writer, "licenseUrl", packageSpec.LicenseUrl);

            if (packageSpec.RequireLicenseAcceptance)
            {
                SetValue(writer, "requireLicenseAcceptance", packageSpec.RequireLicenseAcceptance.ToString());
            }

            if (packOptions.PackageType != null)
            {
                if (packOptions.PackageType.Count == 1)
                {
                    SetValue(writer, JsonPackageSpecReader.PackageType, packOptions.PackageType[0].Name);
                }
                else if (packOptions.PackageType.Count > 1)
                {
                    var packageTypeNames = packOptions.PackageType.Select(p => p.Name);
                    SetArrayValue(writer, JsonPackageSpecReader.PackageType, packageTypeNames);
                }
            }

            writer.WriteObjectEnd();
        }

        private static void SetDependencies(IObjectWriter writer, IList<LibraryDependency> libraryDependencies)
        {
            SetDependencies(writer, "dependencies", libraryDependencies.Where(dependency => dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference));
            SetDependencies(writer, "frameworkAssemblies", libraryDependencies.Where(dependency => dependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference));
        }

        private static void SetDependencies(IObjectWriter writer, string name, IEnumerable<LibraryDependency> libraryDependencies)
        {
            if (!libraryDependencies.Any())
            {
                return;
            }

            writer.WriteObjectStart(name);

            foreach (var dependency in libraryDependencies)
            {
                var expandedMode = dependency.IncludeType != LibraryIncludeFlags.All
                    || dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent
                    || dependency.Type != LibraryDependencyType.Default
                    || (dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference
                        && dependency.LibraryRange.TypeConstraint != (LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference));

                var versionRange = dependency.LibraryRange.VersionRange ?? VersionRange.All;
                var versionString = versionRange.OriginalString;

                if (string.IsNullOrEmpty(versionString))
                {
                    versionString = versionRange.ToNormalizedString();
                }

                if (expandedMode)
                {
                    writer.WriteObjectStart(dependency.Name);

                    if (dependency.IncludeType != LibraryIncludeFlags.All)
                    {
                        SetValue(writer, "include", dependency.IncludeType.ToString());
                    }

                    if (dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent)
                    {
                        SetValue(writer, "suppressParent", dependency.SuppressParent.ToString());
                    }

                    if (dependency.Type != LibraryDependencyType.Default)
                    {
                        SetValue(writer, "type", dependency.Type.ToString());
                    }

                    if (dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference
                        && dependency.LibraryRange.TypeConstraint != (LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference))
                    {
                        SetValue(writer, "target", dependency.LibraryRange.TypeConstraint.ToString());
                    }

                    if (VersionRange.All.Equals(versionRange)
                        && !dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package)
                        && !dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Reference)
                        && !dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.ExternalProject))
                    {
                        // Allow this specific case to skip the version property
                    }
                    else
                    {
                        SetValue(writer, "version", versionString);
                    }

                    writer.WriteObjectEnd();
                }
                else
                {
                    writer.WriteNameValue(dependency.Name, versionString);
                }
            }

            writer.WriteObjectEnd();
        }

        private static void SetImports(IObjectWriter writer, IList<NuGetFramework> frameworks)
        {
            if (frameworks?.Any() == true)
            {
                var imports = frameworks.Select(framework => framework.GetShortFolderName());

                writer.WriteNameArray("imports", imports);
            }
        }

        private static void SetFrameworks(IObjectWriter writer, IList<TargetFrameworkInformation> frameworks)
        {
            if (frameworks.Any())
            {
                writer.WriteObjectStart("frameworks");

                foreach (var framework in frameworks)
                {
                    writer.WriteObjectStart(framework.FrameworkName.GetShortFolderName());

                    SetDependencies(writer, framework.Dependencies);
                    SetImports(writer, framework.Imports);

                    writer.WriteObjectEnd();
                }

                writer.WriteObjectEnd();
            }
        }

        private static void SetValue(IObjectWriter writer, string name, string value)
        {
            if (value != null)
            {
                writer.WriteNameValue(name, value);
            }
        }

        private static void SetArrayValue(IObjectWriter writer, string name, IEnumerable<string> values)
        {
            if (values != null && values.Any())
            {
                writer.WriteNameArray(name, values);
            }
        }

        private static void SetDictionaryValue(IObjectWriter writer, string name, IDictionary<string, string> values)
        {
            if (values != null && values.Any())
            {
                writer.WriteObjectStart(name);

                var sortedValues = values.OrderBy(pair => pair.Key, StringComparer.Ordinal);

                foreach (var pair in sortedValues)
                {
                    writer.WriteNameValue(pair.Key, pair.Value);
                }

                writer.WriteObjectEnd();
            }
        }

        private static void SetDictionaryValues(IObjectWriter writer, string name, IDictionary<string, IEnumerable<string>> values)
        {
            if (values != null && values.Any())
            {
                writer.WriteObjectStart(name);

                var sortedValues = values.OrderBy(pair => pair.Key, StringComparer.Ordinal);

                foreach (var pair in sortedValues)
                {
                    writer.WriteNameArray(pair.Key, pair.Value);
                }

                writer.WriteObjectEnd();
            }
        }
    }
}