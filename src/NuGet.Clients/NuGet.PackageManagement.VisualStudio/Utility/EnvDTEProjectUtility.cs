// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.Build.Evaluation;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using VSLangProj;
using VSLangProj80;
using VsWebSite;
using MSBuildEvaluationProject = Microsoft.Build.Evaluation.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class EnvDTEProjectUtility
    {
        #region Constants and Statics

        private static readonly Dictionary<string, string> KnownNestedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "web.debug.config", "web.config" },
                { "web.release.config", "web.config" },
            };

        private static readonly IEnumerable<string> FileKinds = new[] { VsProjectTypes.VsProjectItemKindPhysicalFile, VsProjectTypes.VsProjectItemKindSolutionItem };
        private static readonly IEnumerable<string> FolderKinds = new[] { VsProjectTypes.VsProjectItemKindPhysicalFolder, VsProjectTypes.TDSItemTypeGuid };

        private static readonly char[] PathSeparatorChars = { Path.DirectorySeparatorChar };

        #endregion // Constants and Statics

        #region Get Project Information

        internal static bool IsSolutionFolder(EnvDTE.Project envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return envDTEProject.Kind != null && envDTEProject.Kind.Equals(VsProjectTypes.VsProjectItemKindSolutionFolder, StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<bool> ContainsFile(EnvDTE.Project envDTEProject, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.Equals(envDTEProject.Kind, VsProjectTypes.WixProjectTypeGuid, StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(envDTEProject.Kind, VsProjectTypes.NemerleProjectTypeGuid, StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(envDTEProject.Kind, VsProjectTypes.FsharpProjectTypeGuid, StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(envDTEProject.Kind, VsProjectTypes.JsProjectTypeGuid, StringComparison.OrdinalIgnoreCase))
            {
                // For Wix and Nemerle projects, IsDocumentInProject() returns not found
                // even though the file is in the project. So we use GetProjectItem()
                // instead. Nemerle is a high-level statically typed programming language for .NET platform
                // Note that pszMkDocument, the document moniker, passed to IsDocumentInProject(), must be a path to the file
                // for certain file-based project systems such as F#. And, not just a filename. For these project systems as well,
                // do the following
                var item = await GetProjectItemAsync(envDTEProject, path);
                return item != null;
            }
            var vsProject = (IVsProject)VsHierarchyUtility.ToVsHierarchy(envDTEProject);
            if (vsProject == null)
            {
                return false;
            }

            int pFound;
            uint itemId;

            if (IsProjectCapabilityCompliant(envDTEProject))
            {
                // REVIEW: We want to revisit this after RTM - the code in this if statement should be applied to every project type.
                // We're checking for VSDOCUMENTPRIORITY.DP_Standard here to see if the file is included in the project.
                // Original check (outside of if) did not have this.
                var priority = new VSDOCUMENTPRIORITY[1];
                var hr = vsProject.IsDocumentInProject(path, out pFound, priority, out itemId);
                return ErrorHandler.Succeeded(hr) && pFound == 1 && priority[0] >= VSDOCUMENTPRIORITY.DP_Standard;
            }

            var hres = vsProject.IsDocumentInProject(path, out pFound, new VSDOCUMENTPRIORITY[0], out itemId);
            return ErrorHandler.Succeeded(hres) && pFound == 1;
        }

        // Get the ProjectItems for a folder path
        public static async Task<EnvDTE.ProjectItems> GetProjectItemsAsync(EnvDTE.Project envDTEProject, string folderPath, bool createIfNotExists)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrEmpty(folderPath))
            {
                return envDTEProject.ProjectItems;
            }

            // Traverse the path to get at the directory
            var pathParts = folderPath.Split(PathSeparatorChars, StringSplitOptions.RemoveEmptyEntries);

            // 'cursor' can contain a reference to either a Project instance or ProjectItem instance. 
            // Both types have the ProjectItems property that we want to access.
            object cursor = envDTEProject;

            var fullPath = EnvDTEProjectInfoUtility.GetFullPath(envDTEProject);
            var folderRelativePath = string.Empty;

            foreach (var part in pathParts)
            {
                fullPath = Path.Combine(fullPath, part);
                folderRelativePath = Path.Combine(folderRelativePath, part);

                cursor = await GetOrCreateFolderAsync(envDTEProject, cursor, fullPath, folderRelativePath, part, createIfNotExists);
                if (cursor == null)
                {
                    return null;
                }
            }

            return GetProjectItems(cursor);
        }

        // 'parentItem' can be either a Project or ProjectItem
        private static async Task<EnvDTE.ProjectItem> GetOrCreateFolderAsync(
            EnvDTE.Project envDTEProject,
            object parentItem,
            string fullPath,
            string folderRelativePath,
            string folderName,
            bool createIfNotExists)
        {
            if (parentItem == null)
            {
                return null;
            }

            EnvDTE.ProjectItem subFolder;

            var envDTEProjectItems = GetProjectItems(parentItem);
            if (TryGetFolder(envDTEProjectItems, folderName, out subFolder))
            {
                // Get the sub folder
                return subFolder;
            }
            if (createIfNotExists)
            {
                // The JS Metro project system has a bug whereby calling AddFolder() to an existing folder that
                // does not belong to the project will throw. To work around that, we have to manually include 
                // it into our project.
                if (EnvDTEProjectInfoUtility.IsJavaScriptProject(envDTEProject)
                    && Directory.Exists(fullPath))
                {
                    var succeeded = await IncludeExistingFolderToProjectAsync(envDTEProject, folderRelativePath);
                    if (succeeded)
                    {
                        // IMPORTANT: after including the folder into project, we need to get 
                        // a new EnvDTEProjecItems snapshot from the parent item. Otherwise, reusing 
                        // the old snapshot from above won't have access to the added folder.
                        envDTEProjectItems = GetProjectItems(parentItem);
                        if (TryGetFolder(envDTEProjectItems, folderName, out subFolder))
                        {
                            // Get the sub folder
                            return subFolder;
                        }
                    }
                    return null;
                }

                try
                {
                    return envDTEProjectItems.AddFromDirectory(fullPath);
                }
                catch (NotImplementedException)
                {
                    // This is the case for F#'s project system, we can't add from directory so we fall back
                    // to this impl
                    return envDTEProjectItems.AddFolder(folderName);
                }
            }

            return null;
        }

        private static bool TryGetFolder(EnvDTE.ProjectItems envDTEProjectItems, string name, out EnvDTE.ProjectItem envDTEProjectItem)
        {
            envDTEProjectItem = GetProjectItem(envDTEProjectItems, name, FolderKinds);

            return envDTEProjectItem != null;
        }

        private static async Task<bool> IncludeExistingFolderToProjectAsync(EnvDTE.Project envDTEProject, string folderRelativePath)
        {
            // Execute command to include the existing folder into project. Must do this on UI thread.
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var projectHierarchy = (IVsUIHierarchy)VsHierarchyUtility.ToVsHierarchy(envDTEProject);

            uint itemId;
            var hr = projectHierarchy.ParseCanonicalName(folderRelativePath, out itemId);
            if (!ErrorHandler.Succeeded(hr))
            {
                return false;
            }

            hr = projectHierarchy.ExecCommand(
                itemId,
                ref VsMenus.guidStandardCommandSet2K,
                (int)VSConstants.VSStd2KCmdID.INCLUDEINPROJECT,
                0,
                IntPtr.Zero,
                IntPtr.Zero);

            return ErrorHandler.Succeeded(hr);
        }

        private static bool TryGetFile(EnvDTE.ProjectItems envDTEProjectItems, string name, out EnvDTE.ProjectItem envDTEProjectItem)
        {
            envDTEProjectItem = GetProjectItem(envDTEProjectItems, name, FileKinds);

            if (envDTEProjectItem == null)
            {
                // Try to get the nested project item
                return TryGetNestedFile(envDTEProjectItems, name, out envDTEProjectItem);
            }

            return envDTEProjectItem != null;
        }

        /// <summary>
        /// If we didn't find the project item at the top level, then we look one more level down.
        /// In VS files can have other nested files like foo.aspx and foo.aspx.cs or web.config and web.debug.config.
        /// These are actually top level files in the file system but are represented as nested project items in VS.
        /// </summary>
        private static bool TryGetNestedFile(EnvDTE.ProjectItems envDTEProjectItems, string name, out EnvDTE.ProjectItem envDTEProjectItem)
        {
            string parentFileName;
            if (!KnownNestedFiles.TryGetValue(name, out parentFileName))
            {
                parentFileName = Path.GetFileNameWithoutExtension(name);
            }

            // If it's not one of the known nested files then we're going to look up prefixes backwards
            // i.e. if we're looking for foo.aspx.cs then we look for foo.aspx then foo.aspx.cs as a nested file
            var parentEnvDTEProjectItem = GetProjectItem(envDTEProjectItems, parentFileName, FileKinds);

            if (parentEnvDTEProjectItem != null)
            {
                // Now try to find the nested file
                envDTEProjectItem = GetProjectItem(parentEnvDTEProjectItem.ProjectItems, name, FileKinds);
            }
            else
            {
                envDTEProjectItem = null;
            }

            return envDTEProjectItem != null;
        }

        [SuppressMessage("Microsoft.Design", "CA1031")]
        private static EnvDTE.ProjectItem GetProjectItem(EnvDTE.ProjectItems envDTEProjectItems, string name, IEnumerable<string> allowedItemKinds)
        {
            try
            {
                var envDTEProjectItem = envDTEProjectItems.Item(name);
                if (envDTEProjectItem != null
                    && allowedItemKinds.Contains(envDTEProjectItem.Kind, StringComparer.OrdinalIgnoreCase))
                {
                    return envDTEProjectItem;
                }
            }
            catch
            {
            }

            return null;
        }

        private static EnvDTE.ProjectItems GetProjectItems(object parent)
        {
            var envDTEProject = parent as EnvDTE.Project;
            if (envDTEProject != null)
            {
                return envDTEProject.ProjectItems;
            }

            var envDTEProjectItem = parent as EnvDTE.ProjectItem;
            if (envDTEProjectItem != null)
            {
                return envDTEProjectItem.ProjectItems;
            }

            return null;
        }

        internal static async Task<EnvDTE.ProjectItem> GetProjectItemAsync(EnvDTE.Project envDTEProject, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var folderPath = Path.GetDirectoryName(path);
            var itemName = Path.GetFileName(path);

            var container = await GetProjectItemsAsync(envDTEProject, folderPath, createIfNotExists: false);

            EnvDTE.ProjectItem projectItem;
            // If we couldn't get the folder, or the child item doesn't exist, return null
            if (container == null
                ||
                (!TryGetFile(container, itemName, out projectItem) &&
                 !TryGetFolder(container, itemName, out projectItem)))
            {
                return null;
            }

            return projectItem;
        }

        internal static async Task<IEnumerable<EnvDTE.ProjectItem>> GetChildItems(EnvDTE.Project envDTEProject, string path, string filter, string desiredKind)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectItems = await GetProjectItemsAsync(envDTEProject, path, createIfNotExists: false);

            if (projectItems == null)
            {
                return Enumerable.Empty<EnvDTE.ProjectItem>();
            }

            var matcher = filter.Equals("*.*", StringComparison.OrdinalIgnoreCase) ? null : GetFilterRegex(filter);

            return from EnvDTE.ProjectItem p in projectItems
                   where desiredKind.Equals(p.Kind, StringComparison.OrdinalIgnoreCase) &&
                         (matcher == null || matcher.IsMatch(p.Name))
                   select p;
        }

        private static Regex GetFilterRegex(string wildcard)
        {
            // Need NOT be on the UI thread

            var pattern = string.Join(string.Empty, wildcard.Split('.').Select(GetPattern));
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        }

        private static string GetPattern(string token)
        {
            return token == "*" ? @"(.*)" : @"(" + token + ")";
        }

        internal static MSBuildEvaluationProject AsMSBuildEvaluationProject(string projectFullName)
        {
            return ProjectCollection.GlobalProjectCollection.GetLoadedProjects(projectFullName).FirstOrDefault() ??
                   ProjectCollection.GlobalProjectCollection.LoadProject(projectFullName);
        }

        internal static References GetReferences(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic projectObj = project.Object;
            var references = (References)projectObj.References;
            projectObj = null;
            return references;
        }

        public static bool IsSupported(EnvDTE.Project envDTEProject)
        {
            Assumes.Present(envDTEProject);

            ThreadHelper.ThrowIfNotOnUIThread();

            if (SupportsProjectKPackageManager(envDTEProject))
            {
                return true;
            }

            if (IsProjectCapabilityCompliant(envDTEProject))
            {
                return true;
            }

            return envDTEProject.Kind != null && SupportedProjectTypes.IsSupported(envDTEProject.Kind) && !HasUnsupportedProjectCapability(envDTEProject);
        }

        private static bool IsProjectCapabilityCompliant(EnvDTE.Project envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(envDTEProject != null);

            var hierarchy = VsHierarchyUtility.ToVsHierarchy(envDTEProject);

            return VsHierarchyUtility.IsProjectCapabilityCompliant(hierarchy);
        }

        public async static Task<NuGetProject> GetNuGetProjectAsync(EnvDTE.Project project, ISolutionManager solutionManager)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(project != null);
            Debug.Assert(solutionManager != null);

            var nuGetProject = await solutionManager.GetNuGetProjectAsync(project.Name);
            if (nuGetProject == null)
            {
                nuGetProject = await solutionManager.GetNuGetProjectAsync(project.UniqueName);
            }
            return nuGetProject;
        }

        internal static AssemblyReferences GetAssemblyReferences(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic projectObj = project.Object;
            var references = (AssemblyReferences)projectObj.References;
            projectObj = null;
            return references;
        }

        /// <summary>
        /// Recursively retrieves all supported child projects of a virtual folder.
        /// </summary>
        /// <param name="project">The root container project</param>
        internal static IEnumerable<EnvDTE.Project> GetSupportedChildProjects(EnvDTE.Project envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!IsSolutionFolder(envDTEProject))
            {
                yield break;
            }

            var containerProjects = new Queue<EnvDTE.Project>();
            containerProjects.Enqueue(envDTEProject);

            while (containerProjects.Any())
            {
                var containerProject = containerProjects.Dequeue();
                foreach (EnvDTE.ProjectItem item in containerProject.ProjectItems)
                {
                    var nestedProject = item.SubProject;
                    if (nestedProject == null)
                    {
                    }
                    else if (IsSupported(nestedProject))
                    {
                        yield return nestedProject;
                    }
                    else if (IsSolutionFolder(nestedProject))
                    {
                        containerProjects.Enqueue(nestedProject);
                    }
                }
            }
        }

        internal static HashSet<string> GetAssemblyClosure(EnvDTE.Project envDTEProject, IDictionary<string, HashSet<string>> visitedProjects)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            HashSet<string> assemblies;
            if (visitedProjects.TryGetValue(envDTEProject.UniqueName, out assemblies))
            {
                return assemblies;
            }

            assemblies = new HashSet<string>(PathComparer.Default);
            visitedProjects.Add(envDTEProject.UniqueName, assemblies);

            var localProjectAssemblies = GetLocalProjectAssemblies(envDTEProject);
            CollectionsUtility.AddRange(assemblies, localProjectAssemblies);

            var referencedProjects = GetReferencedProjects(envDTEProject);
            foreach (var project in referencedProjects)
            {
                var assemblyClosure = GetAssemblyClosure(project, visitedProjects);
                CollectionsUtility.AddRange(assemblies, assemblyClosure);
            }

            return assemblies;
        }

        private static HashSet<string> GetLocalProjectAssemblies(EnvDTE.Project envDTEProject)
        {
            if (EnvDTEProjectInfoUtility.IsWebSite(envDTEProject))
            {
                var websiteLocalAssemblies = GetWebsiteLocalAssemblies(envDTEProject);
                return websiteLocalAssemblies;
            }

            var assemblies = new HashSet<string>(PathComparer.Default);
            References references;
            try
            {
                references = GetReferences(envDTEProject);
            }
            catch (RuntimeBinderException)
            {
                //References property doesn't exist, project does not have references
                references = null;
            }
            if (references != null)
            {
                foreach (Reference reference in references)
                {
                    var reference3 = reference as Reference3;

                    // Get the referenced project from the reference if any
                    // In C++ projects if reference3.Resolved is false reference3.SourceProject will throw.
                    if (reference3 != null
                        && reference3.Resolved
                        && reference.SourceProject == null
                        && reference.CopyLocal
                        && File.Exists(reference.Path))
                    {
                        assemblies.Add(reference.Path);
                    }
                }
            }
            return assemblies;
        }

        private static HashSet<string> GetWebsiteLocalAssemblies(EnvDTE.Project envDTEProject)
        {
            var assemblies = new HashSet<string>(PathComparer.Default);
            var references = GetAssemblyReferences(envDTEProject);
            foreach (AssemblyReference reference in references)
            {
                // For websites only include bin assemblies
                if (reference.ReferencedProject == null
                    &&
                    reference.ReferenceKind == AssemblyReferenceType.AssemblyReferenceBin
                    &&
                    File.Exists(reference.FullPath))
                {
                    assemblies.Add(reference.FullPath);
                }
            }

            // For website projects, we always add .refresh files that point to the corresponding binaries in packages. In the event of bin deployed assemblies that are also GACed,
            // the ReferenceKind is not AssemblyReferenceBin. Consequently, we work around this by looking for any additional assembly declarations specified via .refresh files.
            var envDTEProjectPath = EnvDTEProjectInfoUtility.GetFullPath(envDTEProject);
            CollectionsUtility.AddRange(assemblies, RefreshFileUtility.ResolveRefreshPaths(envDTEProjectPath));

            return assemblies;
        }

        private class PathComparer : IEqualityComparer<string>
        {
            public static readonly PathComparer Default = new PathComparer();

            public bool Equals(string x, string y)
            {
                return Path.GetFileName(x).Equals(Path.GetFileName(y), StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                return Path.GetFileName(obj).ToLowerInvariant().GetHashCode();
            }
        }

        internal static IList<EnvDTE.Project> GetReferencedProjects(EnvDTE.Project envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (EnvDTEProjectInfoUtility.IsWebSite(envDTEProject))
            {
                return GetWebsiteReferencedProjects(envDTEProject);
            }

            var envDTEProjects = new List<EnvDTE.Project>();
            References references;
            try
            {
                references = GetReferences(envDTEProject);
            }
            catch (RuntimeBinderException)
            {
                //References property doesn't exist, project does not have references
                references = null;
            }
            if (references != null)
            {
                foreach (Reference reference in references)
                {
                    var reference3 = reference as Reference3;

                    // Get the referenced project from the reference if any
                    // C++ projects will throw on reference.SourceProject if reference3.Resolved is false.
                    // It's also possible that the referenced project is the project itself 
                    // for C++ projects. In this case this reference should be skipped to avoid circular
                    // references.
                    if (reference3 != null
                        && reference3.Resolved
                        && reference.SourceProject != null
                        && reference.SourceProject != envDTEProject)
                    {
                        envDTEProjects.Add(reference.SourceProject);
                    }
                }
            }
            return envDTEProjects;
        }

        private static IList<EnvDTE.Project> GetWebsiteReferencedProjects(EnvDTE.Project envDTEProject)
        {
            var envDTEProjects = new List<EnvDTE.Project>();
            var references = GetAssemblyReferences(envDTEProject);
            foreach (AssemblyReference reference in references)
            {
                if (reference.ReferencedProject != null)
                {
                    envDTEProjects.Add(reference.ReferencedProject);
                }
            }
            return envDTEProjects;
        }

        #endregion // Get Project Information

        #region Check Project Types

        public static bool IsExplicitlyUnsupported(EnvDTE.Project envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return envDTEProject.Kind == null || SupportedProjectTypes.IsUnsupported(envDTEProject.Kind);
        }

        public static bool IsParentProjectExplicitlyUnsupported(EnvDTE.Project envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (envDTEProject.ParentProjectItem == null
                || envDTEProject.ParentProjectItem.ContainingProject == null)
            {
                // this project is not a child of another project
                return false;
            }

            var parentEnvDTEProject = envDTEProject.ParentProjectItem.ContainingProject;
            return IsExplicitlyUnsupported(parentEnvDTEProject);
        }

        public static bool SupportsProjectKPackageManager(EnvDTE.Project envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectKProject = GetProjectKPackageManager(envDTEProject);
            return projectKProject != null;
        }

        public static INuGetPackageManager GetProjectKPackageManager(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsProject = project as IVsProject;
            if (vsProject == null)
            {
                return null;
            }

            Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider = null;
            vsProject.GetItemContext(
                (uint)VSConstants.VSITEMID.Root,
                out serviceProvider);
            if (serviceProvider == null)
            {
                return null;
            }

            using (var sp = new ServiceProvider(serviceProvider))
            {
                var retValue = sp.GetService(typeof(INuGetPackageManager));
                if (retValue == null)
                {
                    return null;
                }

                if (!(retValue is INuGetPackageManager))
                {
                    // Workaround a bug in Dev14 prereleases where Lazy<INuGetPackageManager> was returned.
                    var properties = retValue.GetType().GetProperties().Where(p => p.Name == "Value");
                    if (properties.Count() == 1)
                    {
                        retValue = properties.First().GetValue(retValue);
                    }
                }

                return retValue as INuGetPackageManager;
            }
        }

        /// <summary>
        /// True if the project has a project.json file, indicating that it is build integrated
        /// </summary>
        public static async Task<bool> HasBuildIntegratedConfig(EnvDTE.Project project)
        {
            var projectNameConfig = ProjectJsonPathUtilities.GetProjectConfigWithProjectName(project.Name);

            var containsProjectJson = await ContainsFile(project, projectNameConfig);

            var containsProjectNameJson = await ContainsFile(
                project,
                ProjectJsonPathUtilities.ProjectConfigFileName);

            return containsProjectJson || containsProjectNameJson;
        }

        /// <summary>
        /// Check if the project has an unsupported project capability, such as, "SharedAssetsProject"
        /// </summary>
        public static bool HasUnsupportedProjectCapability(EnvDTE.Project envDTEProject)
        {
            var hier = VsHierarchyUtility.ToVsHierarchy(envDTEProject);

            return VsHierarchyUtility.HasUnsupportedProjectCapability(hier);
        }

        #endregion // Check Project Types

        #region Act on Project

        internal static void EnsureCheckedOutIfExists(EnvDTE.Project envDTEProject, string root, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var fullPath = FileSystemUtility.GetFullPath(root, path);
            var dte = envDTEProject.DTE;
            if (dte != null)
            {
                DTESourceControlUtility.EnsureCheckedOutIfExists(dte.SourceControl, fullPath);
            }
        }

        internal static async Task<bool> DeleteProjectItemAsync(EnvDTE.Project envDTEProject, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectItem = await GetProjectItemAsync(envDTEProject, path);
            if (projectItem == null)
            {
                return false;
            }

            projectItem.Delete();
            return true;
        }

        #endregion
    }
}
