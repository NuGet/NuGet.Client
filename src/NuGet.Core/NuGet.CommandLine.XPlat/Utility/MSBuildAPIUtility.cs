// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.CommandLine.XPlat
{
    public class MSBuildAPIUtility
    {
        private const string PACKAGE_REFERENCE_TYPE_TAG = "PackageReference";
        private const string VERSION_TAG = "Version";
        private const string FRAMEWORK_TAG = "TargetFramework";
        private const string FRAMEWORKS_TAG = "TargetFrameworks";
        private const string UPDATE_OPERATION = "Update";
        private const string REMOVE_OPERATION = "Remove";

        public ILogger Logger { get; }

        public MSBuildAPIUtility(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            Logger = logger;
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
                throw new Exception(Strings.Error_MsBuildUnableToOpenProject);
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
                throw new Exception(Strings.Error_MsBuildUnableToOpenProject);
            }
            return new Project(projectRootElement, globalProperties, toolsVersion: null);
        }

        /// <summary>
        /// Remove all package references to the project.
        /// </summary>
        /// <param name="projectPath">Path to the csproj file of the project.</param>
        /// <param name="packageDependency">Package Dependency of the package to be removed.</param>
        public int RemovePackageReference(string projectPath, PackageDependency packageDependency)
        {
            var project = GetProject(projectPath);

            var existingPackageReferences = project.ItemsIgnoringCondition
                .Where(item => item.ItemType.Equals(PACKAGE_REFERENCE_TYPE_TAG, StringComparison.OrdinalIgnoreCase) &&
                               item.EvaluatedInclude.Equals(packageDependency.Id, StringComparison.OrdinalIgnoreCase));

            if (existingPackageReferences.Any())
            {
                // We validate that the operation does not remove any imported items
                // If it does then we throw a user friendly exception without making any changes
                ValidateNoImportedItemsAreUpdated(existingPackageReferences, packageDependency, REMOVE_OPERATION);

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
                    packageDependency.Id,
                    REMOVE_OPERATION));
                ProjectCollection.GlobalProjectCollection.UnloadProject(project);

                return 1;
            }
        }

        /// <summary>
        /// Add an unconditional package reference to the project.
        /// </summary>
        /// <param name="projectPath">Path to the csproj file of the project.</param>
        /// <param name="packageDependency">Package Dependency of the package to be added.</param>
        public void AddPackageReference(string projectPath, PackageDependency packageDependency)
        {
            var project = GetProject(projectPath);

            // Here we get package references for any framework.
            // If the project has a conditional reference, then an unconditional reference is not added.

            var existingPackageReferences = GetPackageReferencesForAllFrameworks(project, packageDependency);
            AddPackageReference(project, packageDependency, existingPackageReferences);
            ProjectCollection.GlobalProjectCollection.UnloadProject(project);
        }

        /// <summary>
        /// Add conditional package reference to the project per target framework.
        /// </summary>
        /// <param name="projectPath">Path to the csproj file of the project.</param>
        /// <param name="packageDependency">Package Dependency of the package to be added.</param>
        /// <param name="frameworks">Target Frameworks for which the package reference should be added.</param>
        public void AddPackageReferencePerTFM(string projectPath, PackageDependency packageDependency,
            IEnumerable<string> frameworks)
        {
            foreach (var framework in frameworks)
            {
                var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "TargetFramework", framework } };
                var project = GetProject(projectPath, globalProperties);
                var existingPackageReferences = GetPackageReferences(project, packageDependency);
                AddPackageReference(project, packageDependency, existingPackageReferences, framework);
                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }
        }

        private void AddPackageReference(Project project,
            PackageDependency packageDependency,
            IEnumerable<ProjectItem> existingPackageReferences,
            string framework = null)
        {
            var itemGroups = GetItemGroups(project);

            if (existingPackageReferences.Count() == 0)
            {
                // Add packageReference only if it does not exist.
                var itemGroup = GetItemGroup(project, itemGroups, PACKAGE_REFERENCE_TYPE_TAG) ?? CreateItemGroup(project, framework);
                AddPackageReferenceIntoItemGroup(itemGroup, packageDependency);
            }
            else
            {
                // If the package already has a reference then try to update the reference.
                UpdatePackageReferenceItems(existingPackageReferences, packageDependency);
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
            PackageDependency packageDependency)
        {
            // We validate that the operation does not update any imported items
            // If it does then we throw a user friendly exception without making any changes
            ValidateNoImportedItemsAreUpdated(packageReferencesItems, packageDependency, UPDATE_OPERATION);

            foreach (var packageReferenceItem in packageReferencesItems)
            {
                var packageVersion = packageDependency.VersionRange.OriginalString ??
                    packageDependency.VersionRange.MinVersion.ToString();
                packageReferenceItem.SetMetadataValue(VERSION_TAG, packageVersion);
                Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.Info_AddPkgUpdated,
                    packageDependency.Id,
                    packageVersion,
                    packageReferenceItem.Xml.ContainingProject.FullPath));
            }
        }

        private void AddPackageReferenceIntoItemGroup(ProjectItemGroupElement itemGroup,
            PackageDependency packageDependency)
        {
            var packageVersion = packageDependency.VersionRange.OriginalString ??
                packageDependency.VersionRange.MinVersion.ToString();

            var item = itemGroup.AddItem(PACKAGE_REFERENCE_TYPE_TAG, packageDependency.Id);
            item.AddMetadata(VERSION_TAG, packageVersion, expressAsAttribute: true);

            Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.Info_AddPkgAdded,
                packageDependency.Id,
                packageVersion,
                itemGroup.ContainingProject.FullPath));
        }

        private static void ValidateNoImportedItemsAreUpdated(IEnumerable<ProjectItem> packageReferencesItems,
            PackageDependency packageDependency,
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
                throw new Exception(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_AddPkgFailOnImportEdit,
                    operationType,
                    packageDependency.Id,
                    Environment.NewLine,
                    errors));
            }
        }

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for a specific target framework.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.
        /// The project should have the global property set to have a specific framework</param>
        /// <param name="packageDependency">Dependency of the package.</param>
        /// <returns>List of Items containing the package reference for the package.</returns>
        private static IEnumerable<ProjectItem> GetPackageReferences(Project project, PackageDependency packageDependency)
        {
            return project.AllEvaluatedItems
                .Where(item => item.ItemType.Equals(PACKAGE_REFERENCE_TYPE_TAG, StringComparison.OrdinalIgnoreCase) &&
                               item.EvaluatedInclude.Equals(packageDependency.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for all target frameworks.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.
        /// The project should have the global property set to have a specific framework</param>
        /// <param name="packageDependency">Dependency of the package.</param>
        /// <returns>List of Items containing the package reference for the package.</returns>
        private static IEnumerable<ProjectItem> GetPackageReferencesForAllFrameworks(Project project,
            PackageDependency packageDependency)
        {
            var frameworks = GetProjectFrameworks(project);
            var mergedPackageReferences = new List<ProjectItem>();
            foreach (var framework in frameworks)
            {
                var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "TargetFramework", framework } };
                var projectPerFramework = GetProject(project.FullPath, globalProperties);

                mergedPackageReferences.AddRange(GetPackageReferences(projectPerFramework, packageDependency));
                ProjectCollection.GlobalProjectCollection.UnloadProject(projectPerFramework);
            }
            return mergedPackageReferences;
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
                return ProjectRootElement.Open(filename);
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