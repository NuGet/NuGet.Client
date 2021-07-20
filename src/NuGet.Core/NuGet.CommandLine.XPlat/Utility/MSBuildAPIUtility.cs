// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class MSBuildAPIUtility : IMSBuildAPIUtility
    {
        private const string PACKAGE_REFERENCE_TYPE_TAG = "PackageReference";
        private const string VERSION_TAG = "Version";
        private const string FRAMEWORK_TAG = "TargetFramework";
        private const string FRAMEWORKS_TAG = "TargetFrameworks";
        private const string RESTORE_STYLE_TAG = "RestoreProjectStyle";
        private const string NUGET_STYLE_TAG = "NuGetProjectStyle";
        private const string ASSETS_FILE_PATH_TAG = "ProjectAssetsFile";
        private const string UPDATE_OPERATION = "Update";
        private const string REMOVE_OPERATION = "Remove";
        private const string IncludeAssets = "IncludeAssets";
        private const string PrivateAssets = "PrivateAssets";
        private const string CollectPackageReferences = "CollectPackageReferences";

        public ILogger Logger { get; }

        public MSBuildAPIUtility(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region IMSBuildAPIUtility

        public Project GetProject(string projectCSProjPath)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_MsBuildUnableToOpenProject, projectCSProjPath));
            }
            return new Project(projectRootElement);
        }

        public IEnumerable<string> GetProjectsFromSolution(string solutionPath)
        {
            var sln = SolutionFile.Parse(solutionPath);
            return sln.ProjectsInOrder.Select(p => p.AbsolutePath);
        }

        public bool IsPackageReferenceProject(Project project)
        {
            return (project.GetPropertyValue(RESTORE_STYLE_TAG) == "PackageReference" ||
                    project.GetItems(PACKAGE_REFERENCE_TYPE_TAG).Count != 0 ||
                    project.GetPropertyValue(NUGET_STYLE_TAG) == "PackageReference" ||
                    project.GetPropertyValue(ASSETS_FILE_PATH_TAG) != "");
        }

        public IEnumerable<FrameworkPackages> GetResolvedVersions(
            string projectPath, IEnumerable<string> userInputFrameworks, LockFile assetsFile, bool transitive, bool includeProjects)
        {
            if (userInputFrameworks == null)
            {
                throw new ArgumentNullException(nameof(userInputFrameworks));
            }

            if (projectPath == null)
            {
                throw new ArgumentNullException(nameof(projectPath));
            }

            if (assetsFile == null)
            {
                throw new ArgumentNullException(nameof(assetsFile));
            }

            var resultPackages = new List<FrameworkPackages>();
            var requestedTargetFrameworks = assetsFile.PackageSpec.TargetFrameworks;
            var requestedTargets = assetsFile.Targets;

            // If the user has entered frameworks, we want to filter
            // the targets and frameworks from the assets file
            if (userInputFrameworks.Any())
            {
                //Target frameworks filtering
                var parsedUserFrameworks = userInputFrameworks.Select(f =>
                                               NuGetFramework.Parse(f.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray()[0]));
                requestedTargetFrameworks = requestedTargetFrameworks.Where(tfm => parsedUserFrameworks.Contains(tfm.FrameworkName)).ToList();

                //Assets file targets filtering by framework and RID
                var filteredTargets = new List<LockFileTarget>();
                foreach (var frameworkAndRID in userInputFrameworks)
                {
                    var splitFrameworkAndRID = frameworkAndRID.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
                    // If a / is not present in the string, we get all of the targets that
                    // have matching framework regardless of RID.
                    if (splitFrameworkAndRID.Count() == 1)
                    {
                        filteredTargets.AddRange(requestedTargets.Where(target => target.TargetFramework.Equals(NuGetFramework.Parse(splitFrameworkAndRID[0]))));
                    }
                    else
                    {
                        //RID is present in the user input, so we filter using it as well
                        filteredTargets.AddRange(requestedTargets.Where(target => target.TargetFramework.Equals(NuGetFramework.Parse(splitFrameworkAndRID[0])) &&
                                                                                  target.RuntimeIdentifier != null && target.RuntimeIdentifier.Equals(splitFrameworkAndRID[1], StringComparison.OrdinalIgnoreCase)));
                    }
                }
                requestedTargets = filteredTargets;
            }

            // Filtering the Targets to ignore TargetFramework + RID combination, only keep TargetFramework in requestedTargets.
            // So that only one section will be shown for each TFM.
            requestedTargets = requestedTargets.Where(target => target.RuntimeIdentifier == null).ToList();

            foreach (var target in requestedTargets)
            {
                // Find the tfminformation corresponding to the target to
                // get the top-level dependencies
                TargetFrameworkInformation tfmInformation;

                try
                {
                    tfmInformation = requestedTargetFrameworks.First(tfm => tfm.FrameworkName.Equals(target.TargetFramework));
                }
                catch (Exception)
                {
                    Console.WriteLine(string.Format(Strings.ListPkg_ErrorReadingAssetsFile, assetsFile.Path));
                    return null;
                }

                //The packages for the framework that were retrieved with GetRequestedVersions
                var frameworkDependencies = tfmInformation.Dependencies;
                var projPackages = GetPackageReferencesFromTargets(projectPath, tfmInformation.ToString());
                var topLevelPackages = new List<InstalledPackageReference>();
                var transitivePackages = new List<InstalledPackageReference>();

                foreach (var library in target.Libraries)
                {
                    var matchingPackages = frameworkDependencies.Where(d =>
                        d.Name.Equals(library.Name, StringComparison.OrdinalIgnoreCase)).ToList();

                    var resolvedVersion = library.Version.ToString();

                    //In case we found a matching package in requestedVersions, the package will be
                    //top level.
                    if (matchingPackages.Any())
                    {
                        var topLevelPackage = matchingPackages.Single();
                        InstalledPackageReference installedPackage;

                        //If the package is not auto-referenced, get the version from the project file. Otherwise fall back on the assets file
                        if (!topLevelPackage.AutoReferenced)
                        {
                            try
                            { // In case proj and assets file are not in sync and some refs were deleted
                                installedPackage = projPackages.Where(p => p.Name.Equals(topLevelPackage.Name, StringComparison.Ordinal)).First();
                            }
                            catch (Exception)
                            {
                                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_ErrorReadingReferenceFromProject, projectPath));
                                return null;
                            }
                        }
                        else
                        {
                            var projectFileVersion = topLevelPackage.LibraryRange.VersionRange.ToString();
                            installedPackage = new InstalledPackageReference(library.Name)
                            {
                                OriginalRequestedVersion = projectFileVersion
                            };
                        }

                        installedPackage.ResolvedPackageMetadata = PackageSearchMetadataBuilder
                            .FromIdentity(new PackageIdentity(library.Name, library.Version))
                            .Build();

                        installedPackage.AutoReference = topLevelPackage.AutoReferenced;

                        if (library.Type != "project" || includeProjects)
                        {
                            topLevelPackages.Add(installedPackage);
                        }
                    }
                    // If no matching packages were found, then the package is transitive,
                    // and include-transitive must be used to add the package
                    else if (transitive) // be sure to exclude "project" references here as these are irrelevant
                    {
                        var installedPackage = new InstalledPackageReference(library.Name)
                        {
                            ResolvedPackageMetadata = PackageSearchMetadataBuilder
                                .FromIdentity(new PackageIdentity(library.Name, library.Version))
                                .Build()
                        };

                        if (library.Type != "project" || includeProjects)
                        {
                            transitivePackages.Add(installedPackage);
                        }
                    }
                }

                var frameworkPackages = new FrameworkPackages(
                    target.TargetFramework.GetShortFolderName(),
                    topLevelPackages,
                    transitivePackages);

                resultPackages.Add(frameworkPackages);
            }

            return resultPackages;
        }

        #endregion

        /// <summary>
        /// Opens an MSBuild.Evaluation.Project type from a csproj file with the given global properties.
        /// </summary>
        /// <param name="projectCSProjPath">CSProj file which needs to be evaluated</param>
        /// <param name="globalProperties">Global properties that should be used to evaluate the project while opening.</param>
        /// <returns>MSBuild.Evaluation.Project</returns>
        private static Project GetProject(string projectCSProjPath, IDictionary<string, string> globalProperties)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_MsBuildUnableToOpenProject, projectCSProjPath));
            }
            return new Project(projectRootElement, globalProperties, toolsVersion: null);
        }

        /// <summary>
        /// Remove all package references to the project.
        /// </summary>
        /// <param name="projectPath">Path to the csproj file of the project.</param>
        /// <param name="libraryDependency">Package Dependency of the package to be removed.</param>
        public int RemovePackageReference(string projectPath, LibraryDependency libraryDependency)
        {
            var project = GetProject(projectPath);

            var existingPackageReferences = project.ItemsIgnoringCondition
                .Where(item => item.ItemType.Equals(PACKAGE_REFERENCE_TYPE_TAG, StringComparison.OrdinalIgnoreCase) &&
                               item.EvaluatedInclude.Equals(libraryDependency.Name, StringComparison.OrdinalIgnoreCase));

            if (existingPackageReferences.Any())
            {
                // We validate that the operation does not remove any imported items
                // If it does then we throw a user friendly exception without making any changes
                ValidateNoImportedItemsAreUpdated(existingPackageReferences, libraryDependency, REMOVE_OPERATION);

                project.RemoveItems(existingPackageReferences);
                project.Save();
                ProjectCollection.GlobalProjectCollection.UnloadProject(project);

                return 0;
            }
            else
            {
                Logger.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_UpdatePkgNoSuchPackage,
                    project.FullPath,
                    libraryDependency.Name,
                    REMOVE_OPERATION));
                ProjectCollection.GlobalProjectCollection.UnloadProject(project);

                return 1;
            }
        }

        /// <summary>
        /// Add an unconditional package reference to the project.
        /// </summary>
        /// <param name="projectPath">Path to the csproj file of the project.</param>
        /// <param name="libraryDependency">Package Dependency of the package to be added.</param>
        public void AddPackageReference(string projectPath, LibraryDependency libraryDependency)
        {
            var project = GetProject(projectPath);

            // Here we get package references for any framework.
            // If the project has a conditional reference, then an unconditional reference is not added.

            var existingPackageReferences = GetPackageReferencesForAllFrameworks(project, libraryDependency);
            AddPackageReference(project, libraryDependency, existingPackageReferences);
            ProjectCollection.GlobalProjectCollection.UnloadProject(project);
        }

        /// <summary>
        /// Add conditional package reference to the project per target framework.
        /// </summary>
        /// <param name="projectPath">Path to the csproj file of the project.</param>
        /// <param name="libraryDependency">Package Dependency of the package to be added.</param>
        /// <param name="frameworks">Target Frameworks for which the package reference should be added.</param>
        public void AddPackageReferencePerTFM(string projectPath, LibraryDependency libraryDependency,
            IEnumerable<string> frameworks)
        {
            foreach (var framework in frameworks)
            {
                var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "TargetFramework", framework } };
                var project = GetProject(projectPath, globalProperties);
                var existingPackageReferences = GetPackageReferences(project, libraryDependency);
                AddPackageReference(project, libraryDependency, existingPackageReferences, framework);
                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }
        }

        private void AddPackageReference(Project project,
            LibraryDependency libraryDependency,
            IEnumerable<ProjectItem> existingPackageReferences,
            string framework = null)
        {
            var itemGroups = GetItemGroups(project);

            if (!existingPackageReferences.Any())
            {
                // Add packageReference only if it does not exist.
                var itemGroup = GetItemGroup(itemGroups, PACKAGE_REFERENCE_TYPE_TAG) ?? CreateItemGroup(project, framework);
                AddPackageReferenceIntoItemGroup(itemGroup, libraryDependency);
            }
            else
            {
                // If the package already has a reference then try to update the reference.
                UpdatePackageReferenceItems(existingPackageReferences, libraryDependency);
            }
            project.Save();
        }

        private static IEnumerable<ProjectItemGroupElement> GetItemGroups(Project project)
        {
            return project
                .Items
                .Where(i => !i.IsImported)
                .Select(item => item.Xml.Parent as ProjectItemGroupElement)
                .Distinct();
        }

        /// <summary>
        /// Get an itemGroup that will contains a package reference tag and meets the condition.
        /// </summary>
        /// <param name="itemGroups">List of all item groups in the project</param>
        /// <param name="itemType">An item type tag that must be in the item group. It if PackageReference in this case.</param>
        /// <returns>An ItemGroup, which could be null.</returns>
        private static ProjectItemGroupElement GetItemGroup(IEnumerable<ProjectItemGroupElement> itemGroups,
            string itemType)
        {
            var itemGroup = itemGroups?
                .Where(itemGroupElement => itemGroupElement.Items.Any(item => item.ItemType == itemType))?
                .FirstOrDefault();

            return itemGroup;
        }

        private static ProjectItemGroupElement CreateItemGroup(Project project, string framework = null)
        {
            // Create a new item group and add a condition if given
            var itemGroup = project.Xml.AddItemGroup();
            if (framework != null)
            {
                itemGroup.Condition = GetTargetFrameworkCondition(framework);
            }
            return itemGroup;
        }

        private void UpdatePackageReferenceItems(IEnumerable<ProjectItem> packageReferencesItems,
            LibraryDependency libraryDependency)
        {
            // We validate that the operation does not update any imported items
            // If it does then we throw a user friendly exception without making any changes
            ValidateNoImportedItemsAreUpdated(packageReferencesItems, libraryDependency, UPDATE_OPERATION);

            foreach (var packageReferenceItem in packageReferencesItems)
            {
                var packageVersion = libraryDependency.LibraryRange.VersionRange.OriginalString ??
                    libraryDependency.LibraryRange.VersionRange.MinVersion.ToString();

                packageReferenceItem.SetMetadataValue(VERSION_TAG, packageVersion);

                if (libraryDependency.IncludeType != LibraryIncludeFlags.All)
                {
                    var includeFlags = MSBuildStringUtility.Convert(LibraryIncludeFlagUtils.GetFlagString(libraryDependency.IncludeType));
                    packageReferenceItem.SetMetadataValue(IncludeAssets, includeFlags);
                }

                if (libraryDependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent)
                {
                    var suppressParent = MSBuildStringUtility.Convert(LibraryIncludeFlagUtils.GetFlagString(libraryDependency.SuppressParent));
                    packageReferenceItem.SetMetadataValue(PrivateAssets, suppressParent);
                }

                Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.Info_AddPkgUpdated,
                    libraryDependency.Name,
                    packageVersion,
                    packageReferenceItem.Xml.ContainingProject.FullPath));
            }
        }

        private void AddPackageReferenceIntoItemGroup(ProjectItemGroupElement itemGroup,
            LibraryDependency libraryDependency)
        {
            var packageVersion = libraryDependency.LibraryRange.VersionRange.OriginalString ??
                libraryDependency.LibraryRange.VersionRange.MinVersion.ToString();

            var item = itemGroup.AddItem(PACKAGE_REFERENCE_TYPE_TAG, libraryDependency.Name);
            item.AddMetadata(VERSION_TAG, packageVersion, expressAsAttribute: true);

            if (libraryDependency.IncludeType != LibraryIncludeFlags.All)
            {
                var includeFlags = MSBuildStringUtility.Convert(LibraryIncludeFlagUtils.GetFlagString(libraryDependency.IncludeType));
                item.AddMetadata(IncludeAssets, includeFlags, expressAsAttribute: false);
            }

            if (libraryDependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent)
            {
                var suppressParent = MSBuildStringUtility.Convert(LibraryIncludeFlagUtils.GetFlagString(libraryDependency.SuppressParent));
                item.AddMetadata(PrivateAssets, suppressParent, expressAsAttribute: false);
            }

            Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.Info_AddPkgAdded,
                libraryDependency.Name,
                packageVersion,
                itemGroup.ContainingProject.FullPath));
        }

        private static void ValidateNoImportedItemsAreUpdated(IEnumerable<ProjectItem> packageReferencesItems,
            LibraryDependency libraryDependency,
            string operationType)
        {
            var importedPackageReferences = packageReferencesItems
                .Where(i => i.IsImported)
                .ToArray();

            // Throw if any of the package references to be updated are imported.
            if (importedPackageReferences.Any())
            {
                var errors = new StringBuilder();
                foreach (var importedPackageReference in importedPackageReferences)
                {
                    errors.AppendLine(string.Format(CultureInfo.CurrentCulture,
                        "\t " + Strings.Error_AddPkgErrorStringForImportedEdit,
                        importedPackageReference.ItemType,
                        importedPackageReference.UnevaluatedInclude,
                        importedPackageReference.Xml.ContainingProject.FullPath));
                }
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_AddPkgFailOnImportEdit,
                    operationType,
                    libraryDependency.Name,
                    Environment.NewLine,
                    errors));
            }
        }

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for a specific target framework.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.</param>
        /// <param name="packageId">Name of the package. If empty, returns all package references</param>
        /// <returns>List of Items containing the package reference for the package.
        /// If the libraryDependency is null then it returns all package references</returns>
        private static IEnumerable<ProjectItem> GetPackageReferences(Project project, string packageId)
        {

            var packageReferences = project.AllEvaluatedItems
                .Where(item => item.ItemType.Equals(PACKAGE_REFERENCE_TYPE_TAG, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(packageId))
            {
                return packageReferences;
            }

            return packageReferences
                .Where(item => item.EvaluatedInclude.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for a specific target framework.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.</param>
        /// <param name="libraryDependency">Library dependency to get the name of the package</param>
        /// <returns>List of Items containing the package reference for the package.
        /// If the libraryDependency is null then it returns all package references</returns>
        private static IEnumerable<ProjectItem> GetPackageReferences(Project project, LibraryDependency libraryDependency)
        {
            return GetPackageReferences(project, libraryDependency.Name);
        }

        /// <summary>
        /// Returns all package references after invoking the target CollectPackageReferences.
        /// </summary>
        /// <param name="projectPath"> Path to the project for which the package references have to be obtained.</param>
        /// <param name="framework">Framework to get reference(s) for</param>
        /// <returns>List of Items containing the package reference for the package.
        /// If the libraryDependency is null then it returns all package references</returns>
        private static IEnumerable<InstalledPackageReference> GetPackageReferencesFromTargets(string projectPath, string framework)
        {
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "TargetFramework", framework } };
            var newProject = new ProjectInstance(projectPath, globalProperties, null);
            newProject.Build(new[] { CollectPackageReferences }, new List<Microsoft.Build.Framework.ILogger> { }, out var targetOutputs);

            return targetOutputs.First(e => e.Key.Equals(CollectPackageReferences)).Value.Items.Select(p =>
                new InstalledPackageReference(p.ItemSpec)
                {
                    OriginalRequestedVersion = p.GetMetadata("version"),
                });
        }

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for a specific target framework.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.</param>
        /// <param name="libraryName">Dependency of the package. If null, all references are returned</param>
        /// <param name="framework">Framework to get reference(s) for</param>
        /// <returns>List of Items containing the package reference for the package.
        /// If the libraryDependency is null then it returns all package references</returns>
        private static IEnumerable<ProjectItem> GetPackageReferencesPerFramework(Project project,
           string libraryName, string framework)
        {
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "TargetFramework", framework } };
            var projectPerFramework = GetProject(project.FullPath, globalProperties);

            var packages = GetPackageReferences(projectPerFramework, libraryName);
            ProjectCollection.GlobalProjectCollection.UnloadProject(projectPerFramework);

            return packages;
        }

        /// <summary>
        /// Given a project, a library dependency and a framework, it returns the package references
        /// for the specific target framework
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.</param>
        /// <param name="libraryDependency">Dependency of the package.</param>
        /// <param name="framework">Specific framework to look at</param>
        /// <returns>List of Items containing the package reference for the package.
        /// If the libraryDependency is null then it returns all package references</returns>
        private static IEnumerable<ProjectItem> GetPackageReferencesPerFramework(Project project,
            LibraryDependency libraryDependency, string framework)
        {
            return GetPackageReferencesPerFramework(project, libraryDependency.Name, framework);
        }

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for all target frameworks.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.
        /// The project should have the global property set to have a specific framework</param>
        /// <param name="libraryDependency">Dependency of the package.</param>
        /// <returns>List of Items containing the package reference for the package.
        /// If the libraryDependency is null then it returns all package reference</returns>
        private static IEnumerable<ProjectItem> GetPackageReferencesForAllFrameworks(Project project,
            LibraryDependency libraryDependency)
        {
            var frameworks = GetProjectFrameworks(project);
            var mergedPackageReferences = new List<ProjectItem>();

            foreach (var framework in frameworks)
            {
                mergedPackageReferences.AddRange(GetPackageReferencesPerFramework(project, libraryDependency, framework));
            }
            return mergedPackageReferences;
        }


        private static IEnumerable<string> GetProjectFrameworks(Project project)
        {
            var frameworks = project
                .AllEvaluatedProperties
                .Where(p => p.Name.Equals(FRAMEWORK_TAG, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.EvaluatedValue);

            if (!frameworks.Any())
            {
                var frameworksString = project
                    .AllEvaluatedProperties
                    .Where(p => p.Name.Equals(FRAMEWORKS_TAG, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.EvaluatedValue)
                    .FirstOrDefault();
                frameworks = MSBuildStringUtility.Split(frameworksString);
            }
            return frameworks;
        }

        private static ProjectRootElement TryOpenProjectRootElement(string filename)
        {
            try
            {
                // There is ProjectRootElement.TryOpen but it does not work as expected
                // I.e. it returns null for some valid projects
                return ProjectRootElement.Open(filename, ProjectCollection.GlobalProjectCollection, preserveFormatting: true);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
            }
        }

        private static string GetTargetFrameworkCondition(string targetFramework)
        {
            return string.Format(CultureInfo.CurrentCulture, "'$(TargetFramework)' == '{0}'", targetFramework);
        }
    }
}
