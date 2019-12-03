// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.VCProjectEngine;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using MicrosoftBuildEvaluationProject = Microsoft.Build.Evaluation.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Contains the information specific to a Visual Basic or C# project.
    /// </summary>
    internal class VCProjectSystemServices
        : GlobalProjectServiceProvider
        , INuGetProjectServices
        , IProjectSystemCapabilities
        , IProjectSystemReferencesReader
        , IProjectSystemReferencesService
    {
        private static readonly Array _referenceMetadata;

        private readonly IVsProjectAdapter _vsProjectAdapter;
        private readonly IVsProjectThreadingService _threadingService;
        private readonly Lazy<VCProject> _asVCProject;

        private VCProject AsVCProject => _asVCProject.Value;

        public bool SupportsPackageReferences => true;

        #region INuGetProjectServices

        public IProjectBuildProperties BuildProperties => _vsProjectAdapter.BuildProperties;

        public IProjectSystemCapabilities Capabilities => this;

        public IProjectSystemReferencesReader ReferencesReader => this;

        public IProjectSystemReferencesService References => this;

        public IProjectSystemService ProjectSystem => throw new NotSupportedException();

        public IProjectScriptHostService ScriptService { get; }

        #endregion INuGetProjectServices

        static VCProjectSystemServices()
        {
            _referenceMetadata = Array.CreateInstance(typeof(string), 5);
            _referenceMetadata.SetValue(ProjectItemProperties.IncludeAssets, 0);
            _referenceMetadata.SetValue(ProjectItemProperties.ExcludeAssets, 1);
            _referenceMetadata.SetValue(ProjectItemProperties.PrivateAssets, 2);
            _referenceMetadata.SetValue(ProjectItemProperties.NoWarn, 3);
            _referenceMetadata.SetValue(ProjectItemProperties.GeneratePathProperty, 4);
        }

        public VCProjectSystemServices(
            IVsProjectAdapter vsProjectAdapter,
            IComponentModel componentModel)
            : base(componentModel)
        {
            Assumes.Present(vsProjectAdapter);

            _vsProjectAdapter = vsProjectAdapter;

            _threadingService = GetGlobalService<IVsProjectThreadingService>();
            Assumes.Present(_threadingService);

            _asVCProject = new Lazy<VCProject>(() => vsProjectAdapter.Project.Object as VCProject);

            ScriptService = new VsProjectScriptHostService(vsProjectAdapter, this);
        }

        public async Task<IEnumerable<LibraryDependency>> GetPackageReferencesAsync(
            NuGetFramework targetFramework, CancellationToken _)
        {
            Assumes.Present(targetFramework);

            //await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();



            IEnumerable<LibraryDependency> references = null;

            await ProjectHelper.DoWorkInReadLockAsync(
                _vsProjectAdapter.Project,
                _vsProjectAdapter.VsHierarchy,
                buildProject => references = GetPackageReferencesAsync(buildProject, targetFramework));


            if(references != null)
                return references.ToList();
            else
                return new LibraryDependency[] { };
        }

        private IEnumerable<LibraryDependency> GetPackageReferencesAsync(
            MicrosoftBuildEvaluationProject msBuildEvaluationproject,
            NuGetFramework targetFramework)
        {
            var packageReferences = msBuildEvaluationproject.GetItems("PackageReference");

            if (packageReferences != null && packageReferences.Count != 0)
            {
                var references = packageReferences.Select(installedPackage =>
                {
                    List<string> metadataElements = new List<string>();
                    List<string> metadataValues = new List<string>();


                    foreach (var item in installedPackage.Metadata)
                    {
                        if (item.Name.Equals("Version", StringComparison.OrdinalIgnoreCase) == false)
                        {
                            metadataElements.Add(item.Name);
                            metadataValues.Add(item.EvaluatedValue);
                        }
                    }


                    return new PackageReference(
                            name: installedPackage.EvaluatedInclude,
                            version: installedPackage.GetMetadataValue("Version"),
                            metadataElements: metadataElements.ToArray(),
                            metadataValues: metadataValues.ToArray(),
                            targetNuGetFramework: targetFramework);

                })
                .Where(p => p != null)
                .Select(ToPackageLibraryDependency);

                return references;
            }

            return null;
        }

        //Have PackageReference?
        public static bool HasPackageReference(IVsProjectAdapter _vsProjectAdapter)
        {
            Assumes.Present(_vsProjectAdapter);

            

            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                bool bHasPackageReference = false;

                await ProjectHelper.DoWorkInReadLockAsync(
                    _vsProjectAdapter.Project,
                    _vsProjectAdapter.VsHierarchy,
                    buildProject =>
                    {
                        var packageReferences = buildProject.GetItems("PackageReference");

                        bHasPackageReference = packageReferences != null && packageReferences.Count != 0;
                    });

                return bHasPackageReference;
            });
        }

        public async Task<IEnumerable<ProjectRestoreReference>> GetProjectReferencesAsync(
            Common.ILogger _, CancellationToken __)
        {
            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            //C++ Project not support Reference
            return new ProjectRestoreReference[] { };
        }

        private static LibraryDependency ToPackageLibraryDependency(PackageReference reference)
        {
            var dependency = new LibraryDependency
            {
                AutoReferenced = MSBuildStringUtility.IsTrue(GetReferenceMetadataValue(reference, ProjectItemProperties.IsImplicitlyDefined)),
                GeneratePathProperty = MSBuildStringUtility.IsTrue(GetReferenceMetadataValue(reference, ProjectItemProperties.GeneratePathProperty)),
                LibraryRange = new LibraryRange(
                    name: reference.Name,
                    versionRange: VersionRange.Parse(reference.Version),
                    typeConstraint: LibraryDependencyTarget.Package)
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                GetReferenceMetadataValue(reference, ProjectItemProperties.IncludeAssets),
                GetReferenceMetadataValue(reference, ProjectItemProperties.ExcludeAssets),
                GetReferenceMetadataValue(reference, ProjectItemProperties.PrivateAssets));


            // Add warning suppressions
            foreach (var code in MSBuildStringUtility.GetNuGetLogCodes(GetReferenceMetadataValue(reference, ProjectItemProperties.NoWarn)))
            {
                dependency.NoWarn.Add(code);
            }

            return dependency;
        }

        private static string GetReferenceMetadataValue(PackageReference reference, string metadataElement)
        {
            Assumes.Present(reference);
            Assumes.NotNullOrEmpty(metadataElement);

            if (reference.MetadataElements == null || reference.MetadataValues == null)
            {
                return string.Empty; // no metadata for package
            }

            var index = Array.IndexOf(reference.MetadataElements, metadataElement);
            if (index >= 0)
            {
                return reference.MetadataValues.GetValue(index) as string;
            }

            return string.Empty;
        }

        public async Task AddOrUpdatePackageReferenceAsync(LibraryDependency packageReference, CancellationToken _)
        {
            Assumes.Present(packageReference);

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

            var includeFlags = packageReference.IncludeType;
            var privateAssetsFlag = packageReference.SuppressParent;
            var metadataElements = new List<string>();
            var metadataValues = new List<string>();
            if (includeFlags != LibraryIncludeFlags.All)
            {
                metadataElements.Add(ProjectItemProperties.IncludeAssets);
                metadataValues.Add(LibraryIncludeFlagUtils.GetFlagString(includeFlags).Replace(',', ';'));
            }

            if (privateAssetsFlag != LibraryIncludeFlagUtils.DefaultSuppressParent)
            {
                metadataElements.Add(ProjectItemProperties.PrivateAssets);
                metadataValues.Add(LibraryIncludeFlagUtils.GetFlagString(privateAssetsFlag).Replace(',', ';'));
            }

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();


                await ProjectHelper.DoWorkInWriterLockAsync(
                    _vsProjectAdapter.Project,
                    _vsProjectAdapter.VsHierarchy,
                    buildProject => AddOrUpdatePackageReference(
                        buildProject,
                        packageReference.Name,
                        packageReference.LibraryRange.VersionRange,
                        metadataElements.ToArray(),
                        metadataValues.ToArray()));
            });
        }

        private void AddOrUpdatePackageReference(MicrosoftBuildEvaluationProject msBuildEvaluationproject, string packageName, VersionRange packageVersion, string[] metadataElements, string[] metadataValues)
        {
            //_threadingService.ThrowIfNotOnUIThread();

            // Note that API behavior is:
            // - specify a metadata element name with a value => add/replace that metadata item on the package reference
            // - specify a metadata element name with no value => remove that metadata item from the project reference
            // - don't specify a particular metadata name => if it exists on the package reference, don't change it (e.g. for user defined metadata)

            var packageReferences = msBuildEvaluationproject.GetItems("PackageReference");
            var metadataElementCount = metadataElements.Length < metadataValues.Length ? metadataElements.Length : metadataValues.Length;

            var szPackageVersion = packageVersion.OriginalString ?? packageVersion.ToShortString();

            foreach (ProjectItem packageReferenceProjectItem in packageReferences)
            {
                if (packageReferenceProjectItem.EvaluatedInclude.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                {
                    //Update PackageReference
                    packageReferenceProjectItem.SetMetadataValue("Version", szPackageVersion);

                    for (int i = 0; i != metadataElementCount; ++i)
                    {
                        if (metadataValues[i] == null || metadataValues[i].Length == 0)
                            packageReferenceProjectItem.RemoveMetadata(metadataElements[i]);
                        else
                            packageReferenceProjectItem.SetMetadataValue(metadataElements[i], metadataValues[i]);
                    }

                    msBuildEvaluationproject.ReevaluateIfNecessary();
                    return;
                }
            }

            ProjectItemElement itemElement = null;

            //add new
            if (packageReferences.Count != 0)
            {
                itemElement = msBuildEvaluationproject.Xml.CreateItemElement("PackageReference", packageName);

                var where = packageReferences.Last().Xml;

                where.Parent.InsertAfterChild(itemElement, where);
            }
            else
            {
                var itemGroup = msBuildEvaluationproject.Xml.AddItemGroup();

                itemElement = itemGroup.AddItem("PackageReference", packageName);
            }


            //Set PackageReference
            itemElement.AddMetadata("Version", szPackageVersion);


            for (int i = 0; i != metadataElementCount; ++i)
            {
                if (metadataValues[i] != null && metadataValues[i].Length != 0)
                    itemElement.AddMetadata(metadataElements[i], metadataValues[i]);
            }

            msBuildEvaluationproject.ReevaluateIfNecessary();

            return;
        }

        public async Task RemovePackageReferenceAsync(string packageName)
        {
            Assumes.NotNullOrEmpty(packageName);

  
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            await ProjectHelper.DoWorkInWriterLockAsync(
                _vsProjectAdapter.Project,
                _vsProjectAdapter.VsHierarchy,
                buildProject => RemovePackageReferenceAsync(buildProject, packageName));
        }

        private void RemovePackageReferenceAsync(MicrosoftBuildEvaluationProject msBuildEvaluationproject, string packageName)
        {
            var packageReferences = msBuildEvaluationproject.GetItems("PackageReference");

            foreach (ProjectItem packageReferenceProjectItem in packageReferences)
            {
                if (packageReferenceProjectItem.EvaluatedInclude.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                {
                    var packageReferenceParent = packageReferenceProjectItem.Xml.Parent;

                    packageReferenceParent.RemoveChild(packageReferenceProjectItem.Xml);


                    if (packageReferenceParent.Count == 0)
                    {
                        packageReferenceParent.Parent.RemoveChild(packageReferenceParent);
                    }


                    msBuildEvaluationproject.ReevaluateIfNecessary();

                    break;
                }
            }
        }

        private class PackageReference
        {
            public PackageReference(
                string name,
                string version,
                Array metadataElements,
                Array metadataValues,
                NuGetFramework targetNuGetFramework)
            {
                Name = name;
                Version = version;
                MetadataElements = metadataElements;
                MetadataValues = metadataValues;
                TargetNuGetFramework = targetNuGetFramework;
            }

            public string Name { get; }
            public string Version { get; }
            public Array MetadataElements { get; }
            public Array MetadataValues { get; }
            public NuGetFramework TargetNuGetFramework { get; }
        }
    }
}
