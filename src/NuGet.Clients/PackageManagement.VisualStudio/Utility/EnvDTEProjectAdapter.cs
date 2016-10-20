// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using VSLangProj;
using VSLangProj150;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An adapter for DTE calls, to allow simplified calls and mocking for tests.
    /// </summary>
    public class EnvDTEProjectAdapter : IEnvDTEProjectAdapter
    {
        /// <summary>
        /// The adaptee for this adapter
        /// </summary>
        private readonly Project _project;

        // Interface casts
        private IVsBuildPropertyStorage _asIVsBuildPropertyStorage;
        private IVsHierarchy _asIVsHierarchy;
        private VSProject4 _asVSProject4;

        // Property caches
        private string _projectFullPath;
        private string _baseIntermediatePath;
        private bool? _isLegacyCSProjPackageReferenceProject;

        public EnvDTEProjectAdapter(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            _project = project;
        }

        private IVsHierarchy AsIVsHierarchy
        {
            get
            {
                return _asIVsHierarchy ?? (_asIVsHierarchy = VsHierarchyUtility.ToVsHierarchy(_project));
            }
        }

        private IVsBuildPropertyStorage AsIVsBuildPropertyStorage
        {
            get
            {
                return _asIVsBuildPropertyStorage ?? (_asIVsBuildPropertyStorage = AsIVsHierarchy as IVsBuildPropertyStorage);
            }
        }

        private VSProject4 AsVSProject4
        {
            get
            {
                return _asVSProject4 ?? (_asVSProject4 = _project.Object as VSProject4);
            }
        }

        public bool IsLegacyCSProjPackageReferenceProject
        {
            get
            {
                if (!_isLegacyCSProjPackageReferenceProject.HasValue)
                {
                    // If it's a VSProject4 and not CPS, it's a legacy CSProj package reference project
                    _isLegacyCSProjPackageReferenceProject = !(AsIVsHierarchy?.IsCapabilityMatch("CPS") ?? false) && AsVSProject4 != null;
                }

                return _isLegacyCSProjPackageReferenceProject.Value;
            }
        }

        public string Name
        {
            get
            {
                // Uncached, in case project is renamed
                return _project.Name;
            }
        }

        public string UniqueName
        {
            get
            {
                // Uncached, in case project is renamed
                return _project.UniqueName;
            }
        }

        public string ProjectFullPath
        {
            get
            {
                return string.IsNullOrEmpty(_projectFullPath) ?
                    (_projectFullPath = EnvDTEProjectUtility.GetFullProjectPath(_project)) :
                    _projectFullPath;
            }
        }

        public async Task<string> GetBaseIntermediatePath()
        {
            if (string.IsNullOrEmpty(_baseIntermediatePath))
            {
                if (AsIVsBuildPropertyStorage != null)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var relativeBaseIntermediatePath = GetMSBuildProperty(AsIVsBuildPropertyStorage, "BaseIntermediateOutputPath");
                    if (!string.IsNullOrEmpty(relativeBaseIntermediatePath))
                    {
                        _baseIntermediatePath = Path.Combine(ProjectFullPath, relativeBaseIntermediatePath);
                    }
                }
            }

            return _baseIntermediatePath;
        }

        public async Task<NuGetFramework> GetTargetNuGetFramework()
        {
           // Uncached, in case project file edited
           await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
           return EnvDTEProjectUtility.GetTargetNuGetFramework(_project);
        }

        public async Task<IEnumerable<LegacyCSProjProjectReference>> GetLegacyCSProjProjectReferencesAsync(Array desiredMetadata)
        {
            if (!IsLegacyCSProjPackageReferenceProject)
            {
                throw new InvalidOperationException("Project reference call made on a non-legacy CSProj project");
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return GetLegacyCSProjProjectReferences(desiredMetadata);
        }

        public async Task<IEnumerable<LegacyCSProjPackageReference>> GetLegacyCSProjPackageReferencesAsync(Array desiredMetadata)
        {
            if (!IsLegacyCSProjPackageReferenceProject)
            {
                throw new InvalidOperationException("Package reference call made on a non-legacy CSProj project");
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return GetLegacyCSProjPackageReferences(desiredMetadata);
        }

        public async Task AddOrUpdateLegacyCSProjPackageAsync(string packageName, string packageVersion, string[] metadataElements, string[] metadataValues)
        {
            if (!IsLegacyCSProjPackageReferenceProject || AsVSProject4 == null)
            {
                throw new InvalidOperationException("Cannot add packages to a project which is not a legacy CSProj package reference project");
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //TODO: add metadata
            AsVSProject4.PackageReferences.AddOrUpdate(packageName, packageVersion, metadataElements, metadataValues);
        }

        public async Task RemoveLegacyCSProjPackageAsync(string packageName)
        {
            if (!IsLegacyCSProjPackageReferenceProject || AsVSProject4 == null)
            {
                throw new InvalidOperationException("Cannot remove packages from a project which is not a legacy CSProj package reference project");
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            AsVSProject4.PackageReferences.Remove(packageName);
        }

        private IEnumerable<LegacyCSProjProjectReference> GetLegacyCSProjProjectReferences(Array desiredMetadata)
        {
            //TODO: replace with metadata calls when available
            var metadataElements = Array.CreateInstance(typeof(string), 1);
            metadataElements.SetValue("metadataName0", 0);
            var metadataValues = Array.CreateInstance(typeof(string), 1);
            metadataValues.SetValue("metadataValue0", 0);
            yield return new LegacyCSProjProjectReference()
            {
                UniqueName = "projectFoo",
                MetadataElements = metadataElements,
                MetadataValues = metadataValues
            };
        }

        private IEnumerable<LegacyCSProjPackageReference> GetLegacyCSProjPackageReferences(Array desiredMetadata)
        {
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

                    yield return new LegacyCSProjPackageReference()
                    {
                        Name = installedPackageName,
                        Version = version,
                        MetadataElements = metadataElements,
                        MetadataValues = metadataValues,
                        TargetNuGetFramework = GetTargetNuGetFramework().Result
                    };
                }
            }
        }

        private static string GetMSBuildProperty(IVsBuildPropertyStorage buildPropertyStorage, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string output;
            var result = buildPropertyStorage.GetPropertyValue(
                name,
                string.Empty,
                (uint)_PersistStorageType.PST_PROJECT_FILE,
                out output);

            if (result != NuGetVSConstants.S_OK || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            return output;
        }
    }
}
