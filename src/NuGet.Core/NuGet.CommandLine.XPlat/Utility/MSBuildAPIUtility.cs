// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
{
    public class MSBuildAPIUtility
    {
        private const string PACKAGE_REFERENCE_TYPE_TAG = "PackageReference";
        private const string VERSION_TAG = "Version";
        private const string FRAMEWORK_TAG = "TargetFramework";
        private const string FRAMEWORKS_TAG = "TargetFrameworks";
        private const string ASSETS_DIRECTORY_TAG = "MSBuildProjectExtensionsPath";
        private const string RESTORE_STYLE_TAG = "RestoreProjectStyle";
        private const string UPDATE_OPERATION = "Update";
        private const string REMOVE_OPERATION = "Remove";
        private const string IncludeAssets = "IncludeAssets";
        private const string PrivateAssets = "PrivateAssets";
        
        public ILogger Logger { get; }

        public MSBuildAPIUtility(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Opens an MSBuild.Evaluation.Project type from a csproj file.
        /// </summary>
        /// <param name="projectCSProjPath">CSProj file which needs to be evaluated</param>
        /// <returns>MSBuild.Evaluation.Project</returns>
        private static Project GetProject(string projectCSProjPath)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_MsBuildUnableToOpenProject, projectCSProjPath));
            }
            return new Project(projectRootElement);
        }

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

        public static List<string> GetProjectsFromSolution(string solutionPath)
        {
            var sln = SolutionFile.Parse(solutionPath);
            return sln.ProjectsInOrder.Select(p => p.AbsolutePath).ToList();
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

            if (existingPackageReferences.Count() == 0)
            {
                // Add packageReference only if it does not exist.
                var itemGroup = GetItemGroup(project, itemGroups, PACKAGE_REFERENCE_TYPE_TAG) ?? CreateItemGroup(project, framework);
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
        /// <param name="project">Project from which item group has to be obtained</param>
        /// <param name="itemGroups">List of all item groups in the project</param>
        /// <param name="itemType">An item type tag that must be in the item group. It if PackageReference in this case.</param>
        /// <returns>An ItemGroup, which could be null.</returns>
        private static ProjectItemGroupElement GetItemGroup(Project project, IEnumerable<ProjectItemGroupElement> itemGroups,
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

        public static Dictionary<string, IEnumerable<Tuple<string, string>>> ListPackageReference(string projectPath,
            LibraryDependency libraryDependency,
            IList<string> userInputFrameworks)
        {
            var project = GetProject(projectPath);
            var packageReferencesPerFramework = new Dictionary<string, IEnumerable<Tuple<string, string>>>();
            var projectFrameworkStrings = GetProjectFrameworks(project);
            var projectFrameworks = new HashSet<NuGetFramework>(GetProjectFrameworks(project).Select(f => NuGetFramework.Parse(f)));

            if (!userInputFrameworks.Any())
            {
                var unconditionalpackageReferences = GetPackageReferences(project, libraryDependency);

                foreach (var framework in projectFrameworkStrings)
                {
                    var conditionalpackageReferences = GetPackageReferencesPerFramework(project, libraryDependency, framework);

                    packageReferencesPerFramework.Add(framework,
                        conditionalpackageReferences.Select(i => Tuple.Create(i.EvaluatedInclude, i.GetMetadataValue(VERSION_TAG))));
                }
            }
            else
            {
                foreach (var userInputFramework in userInputFrameworks)
                {
                    var framework = NuGetFramework.Parse(userInputFramework);
                    if (projectFrameworks.Contains(framework))
                    {
                        var conditionalpackageReferences = GetPackageReferencesPerFramework(project, libraryDependency, userInputFramework);

                        packageReferencesPerFramework.Add(userInputFramework,
                            conditionalpackageReferences.Select(i => Tuple.Create(i.EvaluatedInclude, i.GetMetadataValue(VERSION_TAG))));
                    }
                    else
                    {
                        packageReferencesPerFramework.Add(userInputFramework, null);
                    }
                }
            }

            ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            return packageReferencesPerFramework;
        }

        public Dictionary<string, IEnumerable<PRPackage>> GetPackageReferencesFromAssets(string projectPath, IList<string> userInputFrameworks, bool transitive)
        {
            
            var project = GetProject(projectPath);
            var assetsDirectory = project.GetPropertyValue(ASSETS_DIRECTORY_TAG);



            var assetsPath = Path.Combine(assetsDirectory + LockFileFormat.AssetsFileName);
            
            if (!project.GetPropertyValue(RESTORE_STYLE_TAG).Equals(PACKAGE_REFERENCE_TYPE_TAG, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_NotPRProject,
                    projectPath));
                return null;
            }

            if (!File.Exists(assetsPath))
            {
                Logger.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_AssetsFileNotFound,
                    projectPath));
                return null;
            }

            var lockFileFormat = new LockFileFormat();
            var assetsFile = lockFileFormat.Read(assetsPath);

            var requestedVersions = GetRequestedPackages(userInputFrameworks, assetsFile);

            var packagesPerFramework = GetResolvedVersions(userInputFrameworks, assetsFile, requestedVersions, transitive);

            ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            return packagesPerFramework;
        }

        private Dictionary<string, IEnumerable<PRPackage>> GetRequestedPackages(IList<string> userInputFrameworks, LockFile assetsFile)
        {
            var result = new Dictionary<string, IEnumerable<PRPackage>>();

            foreach (var tfm in assetsFile.PackageSpec.TargetFrameworks)
            {
                if (userInputFrameworks.Count == 0 || userInputFrameworks.Contains(tfm.ToString()))
                {
                    result.Add(
                    tfm.ToString(), tfm.Dependencies.Select(d =>
                    new PRPackage { package = d.Name, requestedVer = d.LibraryRange.VersionRange.ToString(), autoRef = d.AutoReferenced }
                    ));
                }

            }

            return result;
        }

        private Dictionary<string, IEnumerable<PRPackage>> GetResolvedVersions(IList<string> userInputFrameworks,
            LockFile assetsFile, Dictionary<string, IEnumerable<PRPackage>> requestedVersions, bool transitive)
        {
            var resultPackages = new Dictionary<string, IEnumerable<PRPackage>>();

            var inputLockFileTargets = new List<LockFileTarget>();
            foreach (var framework in assetsFile.PackageSpec.TargetFrameworks)
            {
                if (userInputFrameworks.Count == 0 || userInputFrameworks.Contains(framework.ToString()))
                {
                    inputLockFileTargets.Add(assetsFile.GetTarget(framework.FrameworkName, null));
                }

            }
             
            foreach (var target in assetsFile.Targets)
            {
                if (!inputLockFileTargets.Contains(target))
                {
                    continue;
                }

                var topLevelPackages = requestedVersions.Where(f => f.Key.Equals(target.TargetFramework.GetShortFolderName(), StringComparison.OrdinalIgnoreCase)).FirstOrDefault().Value;
                var packages = new List<PRPackage>();

                foreach (var library in target.Libraries)
                {
                    var matchingPackages = topLevelPackages.Where(p => p.package.Equals(library.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingPackages.Count() != 0 || transitive)
                    {
                        
                        var resolvedVersion = library.Version.ToString();
                        var packageInfo = new PRPackage { package = library.Name, resolvedVer = library.Version.ToString() };

                        if (matchingPackages.Count() != 0)
                        {
                            var topLevelPackage = matchingPackages.Single();
                            packageInfo = new PRPackage { package = packageInfo.package, resolvedVer = packageInfo.resolvedVer, requestedVer = topLevelPackage.requestedVer, autoRef = topLevelPackage.autoRef };
                        }

                        packages.Add(packageInfo);
                    }

                }

                resultPackages.Add(target.TargetFramework.GetShortFolderName(), packages);
            }

            return resultPackages;
        }

        

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for a specific target framework.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.</param>
        /// <param name="libraryDependency">Dependency of the package.</param>
        /// <returns>List of Items containing the package reference for the package.
        /// If the libraryDependency is null then it returns all package references</returns>
        private static IEnumerable<ProjectItem> GetPackageReferences(Project project, LibraryDependency libraryDependency)
        {
            var packageReferences = project.AllEvaluatedItems
                .Where(item => item.ItemType.Equals(PACKAGE_REFERENCE_TYPE_TAG, StringComparison.OrdinalIgnoreCase));

            if (libraryDependency == null)
            {
                return packageReferences.ToList();
            }
            else
            {
                return packageReferences
                    .Where(item => item.EvaluatedInclude.Equals(libraryDependency.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            }
                              
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
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "TargetFramework", framework } };
            var projectPerFramework = GetProject(project.FullPath, globalProperties);

            var packages = GetPackageReferences(projectPerFramework, libraryDependency);
            ProjectCollection.GlobalProjectCollection.UnloadProject(projectPerFramework);

            return packages;
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

        private static IEnumerable<ProjectItem> GetPackageReferencesForAllFrameworks(string project)
        {
            return GetPackageReferencesForAllFrameworks(GetProject(project), null);
            
        }

        private static IEnumerable<string> GetProjectFrameworks(Project project)
        {
            var frameworks = project
                .AllEvaluatedProperties
                .Where(p => p.Name.Equals(FRAMEWORK_TAG, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.EvaluatedValue);

            if (frameworks.Count() == 0)
            {
                var frameworksString = project
                    .AllEvaluatedProperties
                    .Where(p => p.Name.Equals(FRAMEWORKS_TAG, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.EvaluatedValue)
                    .LastOrDefault();
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
            return string.Format("'$(TargetFramework)' == '{0}'", targetFramework);
        }
    }
}