// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.VisualStudio;
using VSLangProj;
using VSLangProj150;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An adapter for DTE calls, to allow simplified calls and mocking for tests.
    /// </summary>
    public class EnvDTEProjectAdapter : IEnvDTEProjectAdapter
    {
        private const string RestoreProjectStyle = "RestoreProjectStyle";

        /// <summary>
        /// The adaptee for this adapter
        /// </summary>
        private readonly Project _project;

        // Interface casts
        private IVsBuildPropertyStorage _asIVsBuildPropertyStorage;
        private IVsHierarchy _asIVsHierarchy;
        private VSProject _asVSProject;
        private VSProject4 _asVSProject4;

        // Property caches
        private string _projectFullPath;
        private bool? _isLegacyCSProjPackageReferenceProject;

        public EnvDTEProjectAdapter(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            _project = project;
        }

        public Project DTEProject
        {
            get
            {
                // Note that we don't throw when not on UI thread for extracting this object. It is completely uncurated
                // access to the DTE project object, and any code using it must be responsible in its thread management.
                return _project;
            }
        }

        public string Name
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // Uncached, in case project is renamed
                return _project.Name;
            }
        }

        public string UniqueName
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // Uncached, in case project is renamed
                return EnvDTEProjectInfoUtility.GetCustomUniqueName(_project);
            }
        }

        public string ProjectFullPath
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                return string.IsNullOrEmpty(_projectFullPath) ?
                    (_projectFullPath = EnvDTEProjectInfoUtility.GetFullProjectPath(_project)) :
                    _projectFullPath;
            }
        }

        public string Version
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var packageVersion = GetMSBuildProperty(AsIVsBuildPropertyStorage, "PackageVersion");

                if (string.IsNullOrEmpty(packageVersion))
                {
                    packageVersion = GetMSBuildProperty(AsIVsBuildPropertyStorage, "Version");

                    if (string.IsNullOrEmpty(packageVersion))
                    {
                        packageVersion = "1.0.0";
                    }
                }

                return packageVersion;
            }
        }

        public string BaseIntermediateOutputPath
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var baseIntermediateOutputPath = GetMSBuildProperty(AsIVsBuildPropertyStorage, "BaseIntermediateOutputPath");

                if (string.IsNullOrEmpty(baseIntermediateOutputPath))
                {
                    throw new InvalidOperationException(string.Format(
                        Strings.BaseIntermediateOutputPathNotFound,
                        ProjectFullPath));
                }

                var projectDirectory = Path.GetDirectoryName(ProjectFullPath);

                return Path.Combine(projectDirectory, baseIntermediateOutputPath);
            }
        }

        public string RestorePackagesPath
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var restorePackagesPath = GetMSBuildProperty(AsIVsBuildPropertyStorage, "RestorePackagesPath");

                if (string.IsNullOrEmpty(restorePackagesPath))
                {
                    throw new InvalidOperationException(string.Format(
                        Strings.BaseIntermediateOutputPathNotFound,
                        ProjectFullPath));
                }

                var projectDirectory = Path.GetDirectoryName(ProjectFullPath);

                return Path.Combine(projectDirectory, restorePackagesPath);
            }
        }

        public string PackageTargetFallback
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (AsIVsBuildPropertyStorage == null)
                {
                    return String.Empty;
                }

                return GetMSBuildProperty(AsIVsBuildPropertyStorage, "PackageTargetFallback");
            }
        }

        public bool IsLegacyCSProjPackageReferenceProject
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (!_isLegacyCSProjPackageReferenceProject.HasValue)
                {
                    // A legacy CSProj must cast to VSProject4 to manipulate package references
                    if (AsVSProject4 == null)
                    {
                        _isLegacyCSProjPackageReferenceProject = false;
                    }
                    else
                    {
                        // Check for RestoreProjectStyle property
                        var restoreProjectStyle = GetMSBuildProperty(AsIVsBuildPropertyStorage, RestoreProjectStyle);

                        // For legacy csproj, either the RestoreProjectStyle must be set to PackageReference or
                        // project has atleast one package dependency defined as PackageReference
                        if (restoreProjectStyle?.Equals(ProjectStyle.PackageReference.ToString(), StringComparison.OrdinalIgnoreCase) ?? false
                            || (AsVSProject4.PackageReferences?.InstalledPackages?.Length ?? 0) > 0)
                        {
                            _isLegacyCSProjPackageReferenceProject = true;
                        }
                        else
                        {
                            _isLegacyCSProjPackageReferenceProject = false;
                        }
                    }
                }

                return _isLegacyCSProjPackageReferenceProject.Value;
            }
        }

        public NuGetFramework TargetNuGetFramework
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var nugetFramework = NuGetFramework.UnsupportedFramework;
                var projectPath = ProjectFullPath;
                var platformIdentifier = GetMSBuildProperty(AsIVsBuildPropertyStorage, "TargetPlatformIdentifier");
                var platformVersion = GetMSBuildProperty(AsIVsBuildPropertyStorage, "TargetPlatformVersion");
                var platformMinVersion = GetMSBuildProperty(AsIVsBuildPropertyStorage, "TargetPlatformMinVersion");
                var targetFrameworkMoniker = GetMSBuildProperty(AsIVsBuildPropertyStorage, "TargetFrameworkMoniker");

                // Projects supporting TargetFramework and TargetFrameworks are detected before
                // this check. The values can be passed as null here.
                var frameworkStrings = MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                    projectFilePath: projectPath,
                    targetFrameworks: null,
                    targetFramework: null,
                    targetFrameworkMoniker: targetFrameworkMoniker,
                    targetPlatformIdentifier: platformIdentifier,
                    targetPlatformVersion: platformVersion,
                    targetPlatformMinVersion: platformMinVersion,
                    isManagementPackProject: false,
                    isXnaWindowsPhoneProject: false);

                var frameworkString = frameworkStrings.FirstOrDefault();

                if (!string.IsNullOrEmpty(frameworkString))
                {
                    nugetFramework = NuGetFramework.Parse(frameworkString);
                }

                return nugetFramework;
            }
        }

        public IEnumerable<RuntimeDescription> Runtimes
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var unparsedRuntimeIdentifer = GetMSBuildProperty(AsIVsBuildPropertyStorage, "RuntimeIdentifier");
                var unparsedRuntimeIdentifers = GetMSBuildProperty(AsIVsBuildPropertyStorage, "RuntimeIdentifiers");

                var runtimes = Enumerable.Empty<string>();

                if (unparsedRuntimeIdentifer != null)
                {
                    runtimes = runtimes.Concat(new[] { unparsedRuntimeIdentifer });
                }

                if (unparsedRuntimeIdentifers != null)
                {
                    runtimes = runtimes.Concat(unparsedRuntimeIdentifers.Split(';'));
                }

                runtimes = runtimes
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x));

                return runtimes
                    .Select(runtime => new RuntimeDescription(runtime));
            }
        }

        public IEnumerable<CompatibilityProfile> Supports
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (AsIVsBuildPropertyStorage == null)
                {
                    return Enumerable.Empty<CompatibilityProfile>();
                }

                var unparsedRuntimeSupports = GetMSBuildProperty(AsIVsBuildPropertyStorage, "RuntimeSupports");
                
                if (unparsedRuntimeSupports == null)
                {
                    return Enumerable.Empty<CompatibilityProfile>();
                }

                return unparsedRuntimeSupports
                    .Split(';')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(support => new CompatibilityProfile(support));
            }
        }

        private IVsHierarchy AsIVsHierarchy
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                return _asIVsHierarchy ?? (_asIVsHierarchy = VsHierarchyUtility.ToVsHierarchy(_project));
            }
        }

        private IVsBuildPropertyStorage AsIVsBuildPropertyStorage
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var output =
                    _asIVsBuildPropertyStorage ??
                    (_asIVsBuildPropertyStorage = AsIVsHierarchy as IVsBuildPropertyStorage);

                if (output == null)
                {
                    throw new InvalidOperationException(string.Format(
                        Strings.ProjectCouldNotBeCastedToBuildPropertyStorage,
                        ProjectFullPath));
                }

                return output;
            }
        }

        private VSProject AsVSProject
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                return _asVSProject ?? (_asVSProject = _project.Object as VSProject);
            }
        }

        private VSProject4 AsVSProject4
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                return _asVSProject4 ?? (_asVSProject4 = _project.Object as VSProject4);
            }
        }

        public IEnumerable<LegacyCSProjProjectReference> GetLegacyCSProjProjectReferences(Array desiredMetadata)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (Reference6 reference in AsVSProject4.References)
            {
                if (reference.SourceProject != null)
                {
                    Array metadataElements;
                    Array metadataValues;
                    reference.GetMetadata(desiredMetadata, out metadataElements, out metadataValues);

                    yield return new LegacyCSProjProjectReference(
                        uniqueName: reference.SourceProject.FullName,
                        metadataElements: metadataElements,
                        metadataValues: metadataValues);
                }
            }
        }

        public IEnumerable<LegacyCSProjPackageReference> GetLegacyCSProjPackageReferences(Array desiredMetadata)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var installedPackages = AsVSProject4.PackageReferences.InstalledPackages;
            var packageReferences = new List<LegacyCSProjPackageReference>();

            foreach (var installedPackage in installedPackages)
            {
                var installedPackageName = installedPackage as string;
                if (!string.IsNullOrEmpty(installedPackageName))
                {
                    string version;
                    Array metadataElements;
                    Array metadataValues;
                    AsVSProject4.PackageReferences.TryGetReference(installedPackageName, desiredMetadata, out version, out metadataElements, out metadataValues);

                    yield return new LegacyCSProjPackageReference(
                        name: installedPackageName,
                        version: version,
                        metadataElements: metadataElements,
                        metadataValues: metadataValues,
                        targetNuGetFramework: TargetNuGetFramework);
                }
            }
        }

        public void AddOrUpdateLegacyCSProjPackage(string packageName, string packageVersion, string[] metadataElements, string[] metadataValues)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Note that API behavior is:
            // - specify a metadata element name with a value => add/replace that metadata item on the package reference
            // - specify a metadata element name with no value => remove that metadata item from the project reference
            // - don't specify a particular metadata name => if it exists on the package reference, don't change it (e.g. for user defined metadata)
            AsVSProject4.PackageReferences.AddOrUpdate(packageName, packageVersion, metadataElements, metadataValues);
        }

        public void RemoveLegacyCSProjPackage(string packageName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            AsVSProject4.PackageReferences.Remove(packageName);
        }

        private static string GetMSBuildProperty(IVsBuildPropertyStorage buildPropertyStorage, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string output;
            // passing pszConfigName as null since string.Empty causes it to reevaluate
            // for each property read.
            var result = buildPropertyStorage.GetPropertyValue(
                pszPropName: name,
                pszConfigName: null,
                storage: (uint)_PersistStorageType.PST_PROJECT_FILE,
                pbstrPropValue: out output);

            if (result != VSConstants.S_OK || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            return output;
        }
    }
}
