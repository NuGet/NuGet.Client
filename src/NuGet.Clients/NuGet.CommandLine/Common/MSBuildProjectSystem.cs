// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.Common
{
    public class MSBuildProjectSystem : MSBuildUser, IMSBuildNuGetProjectSystem
    {
        private const string TargetName = "EnsureNuGetPackageBuildImports";
        
        public MSBuildProjectSystem(
            string msbuildDirectory,
            string projectFullPath,
            INuGetProjectContext projectContext)
        {
            LoadAssemblies(msbuildDirectory);

            ProjectFileFullPath = projectFullPath;
            ProjectFullPath = Path.GetDirectoryName(projectFullPath);
            Project = GetProject(projectFullPath);
            ProjectName = Path.GetFileName(projectFullPath);
            ProjectUniqueName = projectFullPath;
            NuGetProjectContext = projectContext;
        }

        public INuGetProjectContext NuGetProjectContext { get; private set; }

        /// <summary>
        /// This does not contain the filename, just the path to the directory where the project file exists
        /// </summary>
        public string ProjectFullPath { get; }

        public string ProjectName { get; }

        public string ProjectUniqueName { get; }

        public string ProjectFileFullPath { get; }

        public dynamic VSProject4 { get; }

        private NuGetFramework _targetFramework;

        public NuGetFramework TargetFramework
        {
            get
            {
                if (_targetFramework == null)
                {
                    var frameworkStrings = MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                        projectFilePath: ProjectFileFullPath,
                        targetFrameworks: GetPropertyValue("TargetFrameworks"),
                        targetFramework: GetPropertyValue("TargetFramework"),
                        targetFrameworkMoniker: GetPropertyValue("TargetFrameworkMoniker"),
                        targetPlatformIdentifier: GetPropertyValue("TargetPlatformIdentifier"),
                        targetPlatformVersion: GetPropertyValue("TargetPlatformVersion"),
                        targetPlatformMinVersion: GetPropertyValue("TargetPlatformMinVersion"));

                    // Parse the framework of the project or return unsupported.
                    var frameworks = MSBuildProjectFrameworkUtility.GetProjectFrameworks(frameworkStrings).ToArray();

                    if (frameworks.Length > 0)
                    {
                        _targetFramework = frameworks[0];
                    }
                    else
                    {
                        _targetFramework = NuGetFramework.UnsupportedFramework;
                    }
                }

                return _targetFramework;
            }
        }

        private dynamic Project { get; }

        public void AddBindingRedirects()
        {
            // No-op
        }

        public void AddExistingFile(string path)
        {
            // No-op
        }

        public void AddFile(string path, Stream stream)
        {
            FileSystemUtility.AddFile(ProjectFullPath, path, stream, NuGetProjectContext);
        }

        public void AddFrameworkReference(string name, string packageId)
        {
            // No-op
        }

        public void AddImport(string targetFullPath, ImportLocation location)
        {
            if (targetFullPath == null)
            {
                throw new ArgumentNullException(nameof(targetFullPath));
            }

            var targetRelativePath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(ProjectFullPath), targetFullPath);
            var imports = Project.Xml.Imports;
            bool notImported = true;
            if (imports != null)
            {
                foreach (dynamic import in imports)
                {
                    if (targetRelativePath.Equals(import.Project, StringComparison.OrdinalIgnoreCase))
                    {
                        notImported = false;
                        break;
                    }
                }
            }
            else
            {
                notImported = true;
            }

            if (notImported)
            {
                var pie = Project.Xml.AddImport(targetRelativePath);
                pie.Condition = "Exists('" + targetRelativePath + "')";
                if (location == ImportLocation.Top)
                {
                    // There's no public constructor to create a ProjectImportElement directly.
                    // So we have to cheat by adding Import at the end, then remove it and insert at the beginning
                    pie.Parent.RemoveChild(pie);
                    Project.Xml.InsertBeforeChild(pie, Project.Xml.FirstChild);
                }

                AddEnsureImportedTarget(targetRelativePath);
                Project.ReevaluateIfNecessary();
            }

            Project.Save();
        }

        public void AddReference(string referencePath)
        {
            string fullPath = PathUtility.GetAbsolutePath(ProjectFullPath, referencePath);
            string relativePath = PathUtility.GetRelativePath(Project.FullPath, fullPath);
            string assemblyFileName = Path.GetFileNameWithoutExtension(fullPath);

            try
            {
                // using full qualified assembly name for strong named assemblies
                var assemblyName = AssemblyName.GetAssemblyName(fullPath);
                assemblyFileName = assemblyName.FullName;
            }
            catch (Exception)
            {
                //ignore exception if we weren't able to get assembly strong name, we'll still use assembly file name to add reference
            }

            Project.AddItem(
                "Reference",
                assemblyFileName,
                new[] { new KeyValuePair<string, string>("HintPath", relativePath),
                        new KeyValuePair<string, string>("Private", "True")});
        }

        public void BeginProcessing()
        {
            // No-op outside of visual studio, this is implemented in other project systems, like vsmsbuild & website.
        }

        public void RegisterProcessedFiles(IEnumerable<string> files)
        {
            // No-op outside of visual studio, this is implemented in other project systems, like vsmsbuild & website.
        }

        public void EndProcessing()
        {
            // No-op outside of visual studio, this is implemented in other project systems, like vsmsbuild & website.
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            FileSystemUtility.DeleteDirectory(path, recursive, NuGetProjectContext);
        }

        public Task ExecuteScriptAsync(PackageIdentity identity, string packageInstallPath, string scriptRelativePath, bool throwOnFailure)
        {
            // No-op
            return Task.FromResult(0);
        }

        public bool FileExistsInProject(string path)
        {
            // some ItemTypes which starts with _ are added by various MSBuild tasks for their own purposes
            // and they do not represent content files of the projects. Therefore, we exclude them when checking for file existence.
            foreach (dynamic item in Project.Items)
            {
                // even though the type of Project.Items is ICollection<ProjectItem>, when dynamic is used
                // the type of item is Dictionary.KeyValuePair, instead of ProjectItem. So another foreach
                // is needed to iterate through all project items.
                foreach (dynamic i in item.Value)
                {
                    if (i.EvaluatedInclude.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                         (String.IsNullOrEmpty(i.ItemType) || i.ItemType[0] != '_'))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            path = Path.Combine(ProjectFullPath, path);
            return Directory.EnumerateDirectories(path);
        }

        public IEnumerable<string> GetFiles(string path, string filter, bool recursive)
        {
            path = Path.Combine(ProjectFullPath, path);
            return Directory.EnumerateFiles(path, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }

        public IEnumerable<string> GetFullPaths(string fileName)
        {
            foreach (dynamic projectItem in Project.Items)
            {
                var itemFileName = Path.GetFileName(projectItem.EvaluatedInclude);
                if (string.Equals(fileName, itemFileName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return Path.Combine(ProjectFullPath, projectItem.EvaluatedInclude);
                }
            }
        }

        public dynamic GetPropertyValue(string propertyName)
        {
            return Project.GetPropertyValue(propertyName);
        }

        public bool IsSupportedFile(string path)
        {
            return true;
        }

        public bool ReferenceExists(string name)
        {
            return GetReference(name) != null;
        }

        public void RemoveFile(string path)
        {
            var fullPath = Path.Combine(ProjectFullPath, path);
            FileSystemUtility.DeleteFile(fullPath, NuGetProjectContext);
        }

        public void RemoveImport(string targetFullPath)
        {
            if (targetFullPath == null)
            {
                throw new ArgumentNullException(nameof(targetFullPath));
            }

            var targetRelativePath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(ProjectFullPath), targetFullPath);
            if (Project.Xml.Imports != null)
            {
                // search for this import statement and remove it
                dynamic importElement = null;
                foreach (dynamic import in Project.Xml.Imports)
                {
                    if (targetRelativePath.Equals(import.Project, StringComparison.OrdinalIgnoreCase))
                    {
                        importElement = import;
                        break;
                    }
                }

                if (importElement != null)
                {
                    importElement.Parent.RemoveChild(importElement);
                    RemoveEnsureImportedTarget(targetRelativePath);
                    Project.ReevaluateIfNecessary();
                }
            }

            Project.Save();
        }

        public void RemoveReference(string name)
        {
            dynamic assemblyReference = GetReference(name);
            if (assemblyReference != null)
            {
                Project.RemoveItem(assemblyReference);
            }
        }

        public string ResolvePath(string path)
        {
            return path;
        }

        public void SetNuGetProjectContext(INuGetProjectContext nuGetProjectContext)
        {
            NuGetProjectContext = nuGetProjectContext;
        }

        public void Save()
        {
            Project.Save();
        }

        private IEnumerable<dynamic> GetItems(string itemType, string name)
        {
            foreach (dynamic i in Project.GetItems(itemType))
            {
                if (i.EvaluatedInclude.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    yield return i;
                }
            }
        }

        private dynamic GetReference(string name)
        {
            name = Path.GetFileNameWithoutExtension(name);
            return GetItems("Reference", name)
                .FirstOrDefault(
                    item =>
                    new AssemblyName(item.EvaluatedInclude).Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private void AddEnsureImportedTarget(string targetsPath)
        {
            // get the target
            dynamic targetElement = null;
            foreach (dynamic target in Project.Xml.Targets)
            {
                if (target.Name.Equals(TargetName, StringComparison.OrdinalIgnoreCase))
                {
                    targetElement = target;
                    break;
                }
            }

            // if the target does not exist, create the target
            if (targetElement == null)
            {
                targetElement = Project.Xml.AddTarget(TargetName);

                // PrepareForBuild is used here because BeforeBuild does not work for VC++ projects.
                targetElement.BeforeTargets = "PrepareForBuild";

                var propertyGroup = targetElement.AddPropertyGroup();
                propertyGroup.AddProperty("ErrorText",
                    "This project references NuGet package(s) that are missing on this computer. " +
                    "Enable NuGet Package Restore to download them.  For more information, see http://go.microsoft.com/fwlink/?LinkID=322105." +
                    "The missing file is {0}.");
            }

            var errorTask = targetElement.AddTask("Error");
            errorTask.Condition = "!Exists('" + targetsPath + "')";
            var errorText = string.Format(
                CultureInfo.InvariantCulture,
                @"$([System.String]::Format('$(ErrorText)', '{0}'))",
                targetsPath);
            errorTask.SetParameter("Text", errorText);
        }

        private void RemoveEnsureImportedTarget(string targetsPath)
        {
            dynamic targetElement = null;
            foreach (dynamic target in Project.Xml.Targets)
            {
                if (string.Equals(target.Name, targetsPath, StringComparison.OrdinalIgnoreCase))
                {
                    targetElement = target;
                    break;
                }
            }
            if (targetElement == null)
            {
                return;
            }

            string errorCondition = "!Exists('" + targetsPath + "')";
            dynamic taskElement = null;
            foreach (dynamic task in targetElement.Tasks)
            {
                if (string.Equals(task.Condition, errorCondition, StringComparison.OrdinalIgnoreCase))
                {
                    taskElement = task;
                    break;
                }
            }
            if (taskElement == null)
            {
                return;
            }

            taskElement.Parent.RemoveChild(taskElement);
            if (targetElement.Tasks.Count == 0)
            {
                targetElement.Parent.RemoveChild(targetElement);
            }
        }

        private dynamic GetProject(string projectFile)
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolve);
            dynamic globalProjectCollection = _projectCollectionType
                .GetProperty("GlobalProjectCollection")
                .GetMethod
                .Invoke(null, new object[] { });
            var loadedProjects = globalProjectCollection.GetLoadedProjects(projectFile);
            if (loadedProjects.Count > 0)
            {
                return loadedProjects[0];
            }

            var project = Activator.CreateInstance(
                _projectType,
                new object[] { projectFile });
            return project;
        }
    }
}