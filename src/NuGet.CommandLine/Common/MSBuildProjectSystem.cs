using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.Common
{
    public class MSBuildProjectSystem : IMSBuildNuGetProjectSystem
    {
        private const string TargetName = "EnsureNuGetPackageBuildImports";
        private readonly string _projectDirectory;

        public MSBuildProjectSystem(string projectFullPath, INuGetProjectContext projectContext)
        {
            ProjectFullPath = projectFullPath;
            _projectDirectory = Path.GetDirectoryName(projectFullPath);

        }

        public INuGetProjectContext NuGetProjectContext { get; private set; }

        public string ProjectFullPath { get; }

        public string ProjectName => Path.GetFileNameWithoutExtension(ProjectFullPath);

        public string ProjectUniqueName => ProjectFullPath;

        public NuGetFramework TargetFramework
        {
            get
            {
                string moniker = GetPropertyValue("TargetFrameworkMoniker");
                if (String.IsNullOrEmpty(moniker))
                {
                    return null;
                }
                return new NuGetFramework(moniker);
            }
        }

        private Project Project { get; }

        public void AddBindingRedirects()
        {
            throw new NotImplementedException();
        }

        public void AddExistingFile(string path)
        {
            // No-op
        }

        public void AddFile(string path, Stream stream)
        {
            FileSystemUtility.AddFile(ProjectFullPath, path, stream, NuGetProjectContext);
        }

        public void AddFrameworkReference(string name)
        {
            // No-op
        }

        public void AddImport(string targetFullPath, ImportLocation location)
        {
            if (targetFullPath == null)
            {
                throw new ArgumentNullException(nameof(targetFullPath));
            }

            var targetRelativePath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(_projectDirectory), targetFullPath);

            if (Project.Xml.Imports == null ||
                Project.Xml.Imports.All(import => !targetRelativePath.Equals(import.Project, StringComparison.OrdinalIgnoreCase)))
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
            string fullPath = PathUtility.GetAbsolutePath(_projectDirectory, referencePath);
            string relativePath = PathUtility.GetRelativePath(Project.FullPath, fullPath);
            // REVIEW: Do we need to use the fully qualified the assembly name for strong named assemblies?
            string include = Path.GetFileNameWithoutExtension(fullPath);

            Project.AddItem(
                "Reference",
                include,
                new[] { new KeyValuePair<string, string>("HintPath", relativePath) });
        }

        public void BeginProcessing(IEnumerable<string> files)
        {
            // No-op
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            FileSystemUtility.DeleteDirectory(path, recursive, NuGetProjectContext);
        }

        public void EndProcessing()
        {
            // No-op
        }

        public Task ExecuteScriptAsync(PackageIdentity identity, string packageInstallPath, string scriptRelativePath, NuGetProject nuGetProject, bool throwOnFailure)
        {
            // No-op
            return Task.FromResult(0);
        }

        public bool FileExistsInProject(string path)
        {
            // some ItemTypes which starts with _ are added by various MSBuild tasks for their own purposes
            // and they do not represent content files of the projects. Therefore, we exclude them when checking for file existence.
            return Project.Items.Any(
                i => i.EvaluatedInclude.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                     (String.IsNullOrEmpty(i.ItemType) || i.ItemType[0] != '_'));
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            path = Path.Combine(_projectDirectory, path);
            return Directory.EnumerateDirectories(path);
        }

        public IEnumerable<string> GetFiles(string path, string filter, bool recursive)
        {
            path = Path.Combine(_projectDirectory, path);
            return Directory.EnumerateFiles(path, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
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
            FileSystemUtility.DeleteFile(path, NuGetProjectContext);
        }

        public void RemoveImport(string targetFullPath)
        {
            if (targetFullPath == null)
            {
                throw new ArgumentNullException(nameof(targetFullPath));
            }

            var targetRelativePath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(_projectDirectory), targetFullPath);
            if (Project.Xml.Imports != null)
            {
                // search for this import statement and remove it
                var importElement = Project.Xml.Imports.FirstOrDefault(
                    import => targetRelativePath.Equals(import.Project, StringComparison.OrdinalIgnoreCase));

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
            ProjectItem assemblyReference = GetReference(name);
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

        private IEnumerable<ProjectItem> GetItems(string itemType, string name)
        {
            return Project.GetItems(itemType).Where(i => i.EvaluatedInclude.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        }

        private ProjectItem GetReference(string name)
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
            var targetElement = Project.Xml.Targets.FirstOrDefault(
                target => target.Name.Equals(TargetName, StringComparison.OrdinalIgnoreCase));

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
            var targetElement = Project.Xml.Targets.FirstOrDefault(
                target => string.Equals(target.Name, targetsPath, StringComparison.OrdinalIgnoreCase));
            if (targetElement == null)
            {
                return;
            }

            string errorCondition = "!Exists('" + targetsPath + "')";
            var taskElement = targetElement.Tasks.FirstOrDefault(
                task => string.Equals(task.Condition, errorCondition, StringComparison.OrdinalIgnoreCase));
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

        private static Project GetProject(string projectFile)
        {
            return ProjectCollection.GlobalProjectCollection.GetLoadedProjects(projectFile).FirstOrDefault() ?? new Project(projectFile);
        }
    }
}