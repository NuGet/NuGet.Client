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
using NuGet.ProjectManagement;

namespace NuGet.Common
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public sealed class MSBuildProjectSystem
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        : MSBuildUser
        , IMSBuildProjectSystem
    {
        private const string TargetName = "EnsureNuGetPackageBuildImports";

        private const string TargetFrameworksProperty = "TargetFrameworks";
        private const string TargetFrameworkProperty = "TargetFramework";
        private const string TargetFrameworkMonikerProperty = "TargetFrameworkMoniker";
        private const string TargetPlatformIdentifierProperty = "TargetPlatformIdentifier";
        private const string TargetPlatformVersionProperty = "TargetPlatformVersion";
        private const string TargetPlatformMinVersionProperty = "TargetPlatformMinVersion";

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public MSBuildProjectSystem(
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
            string msbuildDirectory,
            string projectFullPath,
            INuGetProjectContext projectContext)
        {
            LoadAssemblies(msbuildDirectory);

            ProjectFileFullPath = projectFullPath;
            ProjectFullPath = Path.GetDirectoryName(projectFullPath);
            Project = GetProject(projectFullPath);
            ProjectName = Path.GetFileNameWithoutExtension(projectFullPath);
            ProjectUniqueName = projectFullPath;
            NuGetProjectContext = projectContext;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public INuGetProjectContext NuGetProjectContext { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// This does not contain the filename, just the path to the directory where the project file exists
        /// </summary>
        public string ProjectFullPath { get; }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ProjectName { get; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ProjectUniqueName { get; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ProjectFileFullPath { get; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public dynamic VSProject4 { get; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        private NuGetFramework _targetFramework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public NuGetFramework TargetFramework
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get
            {
                if (_targetFramework == null)
                {
                    var frameworkStrings = MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                        projectFilePath: ProjectFileFullPath,
                        targetFrameworks: GetPropertyValue(TargetFrameworksProperty),
                        targetFramework: GetPropertyValue(TargetFrameworkProperty),
                        targetFrameworkMoniker: GetPropertyValue(TargetFrameworkMonikerProperty),
                        targetPlatformIdentifier: GetPropertyValue(TargetPlatformIdentifierProperty),
                        targetPlatformVersion: GetPropertyValue(TargetPlatformVersionProperty),
                        targetPlatformMinVersion: GetPropertyValue(TargetPlatformMinVersionProperty));

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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void AddBindingRedirects()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            // No-op
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void AddExistingFile(string path)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            // No-op
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void AddFile(string path, Stream stream)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            FileSystemUtility.AddFile(ProjectFullPath, path, stream, NuGetProjectContext);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Task AddFrameworkReferenceAsync(string name, string packageId)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            // No-op
            return Task.CompletedTask;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void AddImport(string targetFullPath, ImportLocation location)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (targetFullPath == null)
            {
                throw new ArgumentNullException(nameof(targetFullPath));
            }

            var targetRelativePath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(ProjectFullPath), targetFullPath);
            var imports = Project.Xml.Imports;
            var notImported = true;
            if (imports != null)
            {
                foreach (var import in imports)
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Task AddReferenceAsync(string referencePath)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var fullPath = PathUtility.GetAbsolutePath(ProjectFullPath, referencePath);
            var relativePath = PathUtility.GetRelativePath(Project.FullPath, fullPath);
            var assemblyFileName = Path.GetFileNameWithoutExtension(fullPath);

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

            return Task.CompletedTask;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Task BeginProcessingAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            // No-op outside of visual studio, this is implemented in other project systems, like vsmsbuild & website.
            return Task.CompletedTask;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void RegisterProcessedFiles(IEnumerable<string> files)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            // No-op outside of visual studio, this is implemented in other project systems, like vsmsbuild & website.
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Task EndProcessingAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            // No-op outside of visual studio, this is implemented in other project systems, like vsmsbuild & website.
            return Task.CompletedTask;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void DeleteDirectory(string path, bool recursive)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            FileSystemUtility.DeleteDirectory(path, recursive, NuGetProjectContext);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool FileExistsInProject(string path)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            // some ItemTypes which starts with _ are added by various MSBuild tasks for their own purposes
            // and they do not represent content files of the projects. Therefore, we exclude them when checking for file existence.
            foreach (var item in Project.Items)
            {
                // even though the type of Project.Items is ICollection<ProjectItem>, when dynamic is used
                // the type of item is Dictionary.KeyValuePair, instead of ProjectItem. So another foreach
                // is needed to iterate through all project items.
                foreach (var i in item.Value)
                {
                    if (i.EvaluatedInclude.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                         (string.IsNullOrEmpty(i.ItemType) || i.ItemType[0] != '_'))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public IEnumerable<string> GetDirectories(string path)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            path = Path.Combine(ProjectFullPath, path);
            return Directory.EnumerateDirectories(path);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public IEnumerable<string> GetFiles(string path, string filter, bool recursive)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            path = Path.Combine(ProjectFullPath, path);
            return Directory.EnumerateFiles(path, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public IEnumerable<string> GetFullPaths(string fileName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            foreach (var projectItem in Project.Items)
            {
                var itemFileName = Path.GetFileName(projectItem.EvaluatedInclude);
                if (string.Equals(fileName, itemFileName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return Path.Combine(ProjectFullPath, projectItem.EvaluatedInclude);
                }
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public dynamic GetPropertyValue(string propertyName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return Project.GetPropertyValue(propertyName);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool IsSupportedFile(string path)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return true;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Task<bool> ReferenceExistsAsync(string name)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return TaskResult.Boolean(GetReference(name) != null);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void RemoveFile(string path)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var fullPath = Path.Combine(ProjectFullPath, path);
            FileSystemUtility.DeleteFile(fullPath, NuGetProjectContext);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void RemoveImport(string targetFullPath)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (targetFullPath == null)
            {
                throw new ArgumentNullException(nameof(targetFullPath));
            }

            var targetRelativePath = PathUtility.GetPathWithForwardSlashes(
                PathUtility.GetRelativePath(
                    PathUtility.EnsureTrailingSlash(ProjectFullPath), targetFullPath));

            if (Project.Xml.Imports != null)
            {
                // search for this import statement and remove it
                dynamic importElement = null;
                foreach (var import in Project.Xml.Imports)
                {
                    var currentPath = PathUtility.GetPathWithForwardSlashes(import.Project);

                    if (StringComparer.OrdinalIgnoreCase.Equals(targetRelativePath, currentPath))
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Task RemoveReferenceAsync(string name)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            dynamic assemblyReference = GetReference(name);
            if (assemblyReference != null)
            {
                Project.RemoveItem(assemblyReference);
            }

            return Task.CompletedTask;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ResolvePath(string path)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return path;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void Save()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            Project.Save();
        }

        private IEnumerable<dynamic> GetItems(string itemType, string name)
        {
            foreach (var i in Project.GetItems(itemType))
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
            foreach (var target in Project.Xml.Targets)
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
            foreach (var target in Project.Xml.Targets)
            {
                if (string.Equals(target.Name, TargetName, StringComparison.OrdinalIgnoreCase))
                {
                    targetElement = target;
                    break;
                }
            }
            if (targetElement == null)
            {
                return;
            }

            var errorCondition = "!Exists('" + PathUtility.GetPathWithForwardSlashes(targetsPath) + "')";
            dynamic taskElement = null;
            foreach (var task in targetElement.Tasks)
            {
                // Compare using / for both paths for mono compat.
                var currentCondition = PathUtility.GetPathWithForwardSlashes(task.Condition);

                if (string.Equals(currentCondition, errorCondition, StringComparison.OrdinalIgnoreCase))
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
            if (((System.Collections.ICollection)targetElement.Tasks).Count == 0)
            {
                targetElement.Parent.RemoveChild(targetElement);
            }
        }

        private dynamic GetProject(string projectFile)
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolve);
            try
            {
                dynamic globalProjectCollection = _projectCollectionType
                    .GetProperty("GlobalProjectCollection")
                    .GetMethod
                    .Invoke(null, Array.Empty<object>());
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
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(AssemblyResolve);
            }
        }
    }
}
