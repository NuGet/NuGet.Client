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
using NuGet.ProjectManagement;
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
        private VSProject _asVSProject;
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

        public bool IsLegacyCSProjPackageReferenceProject
        {
            get
            {
                if (!_isLegacyCSProjPackageReferenceProject.HasValue)
                {
                    // A legacy CSProj can't be CPS, must cast to VSProject4 and *must* have at least one package
                    // reference already in the CSProj. In the future this logic may change. For now a user must
                    // hand code their first package reference. Laid out in longhand for readability.
                    if (AsIVsHierarchy?.IsCapabilityMatch("CPS") ?? true)
                    {
                        _isLegacyCSProjPackageReferenceProject = false;
                    }
                    else if (AsVSProject4 == null)
                    {
                        _isLegacyCSProjPackageReferenceProject = false;
                    }
                    else if ((AsVSProject4.PackageReferences?.InstalledPackages?.Length ?? 0) == 0)
                    {
                        _isLegacyCSProjPackageReferenceProject = false;
                    }
                    else
                    {
                        _isLegacyCSProjPackageReferenceProject = true;
                    }
                }

                return _isLegacyCSProjPackageReferenceProject.Value;
            }
        }

        public Project DTEProject
        {
            get
            {
                return _project;
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

        public string BaseIntermediatePath
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (string.IsNullOrEmpty(_baseIntermediatePath))
                {
                    if (AsIVsBuildPropertyStorage != null)
                    {
                        var intermediateDirectory = GetMSBuildProperty(AsIVsBuildPropertyStorage, "BaseIntermediateOutputPath");
                        var projectDirectory = Path.GetDirectoryName(ProjectFullPath);
                        if (string.IsNullOrEmpty(intermediateDirectory))
                        {
                            _baseIntermediatePath = Path.GetDirectoryName(projectDirectory);
                        }
                        else
                        {
                            if (!Path.IsPathRooted(intermediateDirectory))
                            {
                                _baseIntermediatePath = Path.Combine(projectDirectory, intermediateDirectory);
                            }
                        }
                    }
                }

                return _baseIntermediatePath;
            }
        }

        public bool SupportsReferences
        {
            get
            {
                return EnvDTEProjectUtility.SupportsReferences(_project);
            }
        }

        public IEnumerable<IDependencyGraphProject> ReferencedDependencyGraphProjects
        {
            get
            {
                var references = EnvDTEProjectUtility.GetReferencedProjects(_project);

                foreach(var reference in references)
                {
                    var dependencyGraphProject = reference as IDependencyGraphProject;
                    if (dependencyGraphProject != null)
                    {
                        yield return dependencyGraphProject;
                    }
                }
            }
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

        private VSProject AsVSProject
        {
            get
            {
                return _asVSProject ?? (_asVSProject = _project.Object as VSProject);
            }
        }

        private VSProject4 AsVSProject4
        {
            get
            {
                return _asVSProject4 ?? (_asVSProject4 = _project.Object as VSProject4);
            }
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
            foreach (Reference reference in AsVSProject.References)
            {
                if (reference.SourceProject != null)
                {
                    yield return new LegacyCSProjProjectReference()
                    {
                        UniqueName = reference.SourceProject.FullName
                        // When metadata API is available, each project's metadata can be inserted into this instance
                    };
                }
            }
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
