// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using VSLangProj;
using VSLangProj80;
using VsWebSite;
using Constants = NuGet.ProjectManagement.Constants;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProjectItem = EnvDTE.ProjectItem;
using EnvDTEProjectItems = EnvDTE.ProjectItems;
using EnvDTEProperty = EnvDTE.Property;
using MicrosoftBuildEvaluationProject = Microsoft.Build.Evaluation.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class EnvDTEProjectUtility
    {
        #region Constants and Statics

        public const string NuGetSolutionSettingsFolder = ".nuget";
        public const string PackageReferenceFile = "packages.config";

        private static readonly HashSet<string> SupportedProjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NuGetVSConstants.WebSiteProjectTypeGuid,
                NuGetVSConstants.CsharpProjectTypeGuid,
                NuGetVSConstants.VbProjectTypeGuid,
                NuGetVSConstants.CppProjectTypeGuid,
                NuGetVSConstants.JsProjectTypeGuid,
                NuGetVSConstants.FsharpProjectTypeGuid,
                NuGetVSConstants.NemerleProjectTypeGuid,
                NuGetVSConstants.WixProjectTypeGuid,
                NuGetVSConstants.SynergexProjectTypeGuid,
                NuGetVSConstants.NomadForVisualStudioProjectTypeGuid,
                NuGetVSConstants.TDSProjectTypeGuid,
                NuGetVSConstants.DxJsProjectTypeGuid,
                NuGetVSConstants.DeploymentProjectTypeGuid,
                NuGetVSConstants.CosmosProjectTypeGuid,
                NuGetVSConstants.ManagementPackProjectTypeGuid,
            };

        private static readonly HashSet<string> UnsupportedProjectCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SharedAssetsProject", // This is true for shared projects in universal apps
        };

        public const string WebConfig = "web.config";
        public const string AppConfig = "app.config";
        private const string BinFolder = "Bin";

        private static readonly Dictionary<string, string> KnownNestedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "web.debug.config", "web.config" },
                { "web.release.config", "web.config" },
            };

        private static readonly HashSet<string> UnsupportedProjectTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NuGetVSConstants.LightSwitchProjectTypeGuid,
                NuGetVSConstants.InstallShieldLimitedEditionTypeGuid,
            };

        private static readonly IEnumerable<string> FileKinds = new[] { NuGetVSConstants.VsProjectItemKindPhysicalFile, NuGetVSConstants.VsProjectItemKindSolutionItem };
        private static readonly IEnumerable<string> FolderKinds = new[] { NuGetVSConstants.VsProjectItemKindPhysicalFolder, NuGetVSConstants.TDSItemTypeGuid };

        // List of project types that cannot have references added to them
        private static readonly string[] UnsupportedProjectTypesForAddingReferences =
            {
                NuGetVSConstants.WixProjectTypeGuid,
                NuGetVSConstants.CppProjectTypeGuid,
            };

        // List of project types that cannot have binding redirects added
        private static readonly string[] UnsupportedProjectTypesForBindingRedirects =
            {
                NuGetVSConstants.WixProjectTypeGuid,
                NuGetVSConstants.JsProjectTypeGuid,
                NuGetVSConstants.NemerleProjectTypeGuid,
                NuGetVSConstants.CppProjectTypeGuid,
                NuGetVSConstants.SynergexProjectTypeGuid,
                NuGetVSConstants.NomadForVisualStudioProjectTypeGuid,
                NuGetVSConstants.DxJsProjectTypeGuid,
                NuGetVSConstants.CosmosProjectTypeGuid,
            };

        private static readonly char[] PathSeparatorChars = { Path.DirectorySeparatorChar };

        #endregion // Constants and Statics

        #region Get "Project" Information

        /// <summary>
        /// Returns the full path including the project file name.
        /// </summary>
        internal static string GetFullProjectPath(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(envDTEProject != null);
            if (IsUnloaded(envDTEProject))
            {
                // Find the project file path from the UniqueName which contains the file path 
                // relative to the solution directory for unloaded projects.
                var solutionDirectory = Path.GetDirectoryName(envDTEProject.DTE.Solution.FullName);
                return Path.Combine(solutionDirectory, envDTEProject.UniqueName);
            }

            // FullPath
            var fullPath = GetPotentialFullPathOrNull(GetPropertyValue<string>(envDTEProject, "FullPath"));

            if (fullPath != null)
            {
                return fullPath;
            }

            // FullName
            var fullName = GetPotentialFullPathOrNull(envDTEProject.FullName);

            if (fullName != null)
            {
                return fullName;
            }

            return null;
        }

        public static string GetProjectDirectory(EnvDTEProject envDTEProject)
        {
            return GetFullPath(envDTEProject);
        }

        /// <summary>
        /// Returns the full path of the project directory.
        /// </summary>
        /// <param name="envDTEProject">The project.</param>
        /// <returns>The full path of the project directory.</returns>
        public static string GetFullPath(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(envDTEProject != null);
            if (IsUnloaded(envDTEProject))
            {
                // To get the directory of an unloaded project, we use the UniqueName property,
                // which is the path of the project file relative to the solution directory.
                var solutionDirectory = Path.GetDirectoryName(envDTEProject.DTE.Solution.FullName);
                var projectFileFullPath = Path.Combine(solutionDirectory, envDTEProject.UniqueName);
                return Path.GetDirectoryName(projectFileFullPath);
            }

            // Attempt to determine the project path using the available EnvDTE.Project properties.
            // Project systems using async load such as CPS may not have all properties populated 
            // for start up scenarios such as VS Templates. In these cases we need to fallback 
            // until we can find one containing the full path.

            // FullPath
            string fullPath = GetPropertyValue<string>(envDTEProject, "FullPath");

            if (!String.IsNullOrEmpty(fullPath))
            {
                // Some Project System implementations (JS metro app) return the project 
                // file as FullPath. We only need the parent directory
                if (File.Exists(fullPath))
                {
                    return Path.GetDirectoryName(fullPath);
                }

                return fullPath;
            }

            // C++ projects do not have FullPath property, but do have ProjectDirectory one.
            string projectDirectory = GetPropertyValue<string>(envDTEProject, "ProjectDirectory");

            if (!String.IsNullOrEmpty(projectDirectory))
            {
                return projectDirectory;
            }

            // FullName
            if (!String.IsNullOrEmpty(envDTEProject.FullName))
            {
                return Path.GetDirectoryName(envDTEProject.FullName);
            }

            Debug.Fail("Unable to find the project path");

            return null;
        }

        internal static References GetReferences(EnvDTEProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic projectObj = project.Object;
            var references = (References)projectObj.References;
            projectObj = null;
            return references;
        }

        public static bool IsSupported(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(envDTEProject != null);

            if (SupportsINuGetProjectSystem(envDTEProject))
            {
                return true;
            }

            if (IsProjectCapabilityCompliant(envDTEProject))
            {
                return true;
            }

            return envDTEProject.Kind != null && SupportedProjectTypes.Contains(envDTEProject.Kind) && !HasUnsupportedProjectCapability(envDTEProject);
        }

        private static bool IsProjectCapabilityCompliant(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(envDTEProject != null);

            var hierarchy = VsHierarchyUtility.ToVsHierarchy(envDTEProject);

            return hierarchy.IsCapabilityMatch("AssemblyReferences + DeclaredSourceItems + UserSourceItems");
        }

        internal static bool IsSolutionFolder(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return envDTEProject.Kind != null && envDTEProject.Kind.Equals(NuGetVSConstants.VsProjectItemKindSolutionFolder, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUnloaded(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return NuGetVSConstants.UnloadedProjectTypeGuid.Equals(envDTEProject.Kind, StringComparison.OrdinalIgnoreCase);
        }

        public static EnvDTEProject GetActiveProject(IVsMonitorSelection vsMonitorSelection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IntPtr ppHier = IntPtr.Zero;
            uint pitemid;
            IVsMultiItemSelect ppMIS;
            IntPtr ppSC = IntPtr.Zero;

            try
            {
                vsMonitorSelection.GetCurrentSelection(out ppHier, out pitemid, out ppMIS, out ppSC);

                if (ppHier == IntPtr.Zero)
                {
                    return null;
                }

                // multiple items are selected.
                if (pitemid == (uint)VSConstants.VSITEMID.Selection)
                {
                    return null;
                }

                IVsHierarchy hierarchy = Marshal.GetTypedObjectForIUnknown(ppHier, typeof(IVsHierarchy)) as IVsHierarchy;
                if (hierarchy != null)
                {
                    object project;
                    if (hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out project) >= 0)
                    {
                        return project as EnvDTEProject;
                    }
                }

                return null;
            }
            finally
            {
                if (ppHier != IntPtr.Zero)
                {
                    Marshal.Release(ppHier);
                }
                if (ppSC != IntPtr.Zero)
                {
                    Marshal.Release(ppSC);
                }
            }
        }

        public static NuGetProject GetNuGetProject(EnvDTEProject project, ISolutionManager solutionManager)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(project != null);
            Debug.Assert(solutionManager != null);

            var nuGetProject = solutionManager.GetNuGetProject(project.Name);
            if (nuGetProject == null)
            {
                nuGetProject = solutionManager.GetNuGetProject(project.UniqueName);
            }
            return nuGetProject;
        }

        private static T GetPropertyValue<T>(EnvDTEProject envDTEProject, string propertyName)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            Debug.Assert(envDTEProject != null);
            if (envDTEProject.Properties == null)
            {
                // this happens in unit tests
                return default(T);
            }

            try
            {
                EnvDTEProperty property = envDTEProject.Properties.Item(propertyName);
                if (property != null)
                {
                    return (T)property.Value;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidCastException)
            {
            }
            return default(T);
        }

        internal static AssemblyReferences GetAssemblyReferences(EnvDTEProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic projectObj = project.Object;
            var references = (AssemblyReferences)projectObj.References;
            projectObj = null;
            return references;
        }

        /// <summary>
        /// Returns the full path of the packages config file associated with the project.
        /// </summary>
        /// <param name="envDTEProject">The project.</param>
        internal static string GetPackagesConfigFullPath(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(envDTEProject != null);
            var projectDirectory = GetFullPath(envDTEProject);

            var packagesConfigFullPath = Path.Combine(
                projectDirectory ?? String.Empty,
                Constants.PackageReferenceFile);

            return packagesConfigFullPath;
        }

        /// <summary>
        /// Returns the full path of packages.
        /// <projectName>
        /// .config
        /// For example, if project is called "ConsoleApp1", return value is packages.ConsoleApp1.config
        /// </summary>
        /// <param name="envDTEProject">The project.</param>
        internal static string GetPackagesConfigWithProjectNameFullPath(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(envDTEProject != null);
            var projectDirectory = GetFullPath(envDTEProject);

            var packagesConfigWithProjectNameFullPath = Path.Combine(
                projectDirectory ?? String.Empty,
                "packages." + GetName(envDTEProject) + ".config");

            return packagesConfigWithProjectNameFullPath;
        }

        public static string GetName(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string name = envDTEProject.Name;
            if (IsJavaScriptProject(envDTEProject))
            {
                // The JavaScript project initially returns a "(loading..)" suffix to the project Name.
                // Need to get rid of it for the rest of NuGet to work properly.
                // TODO: Follow up with the VS team to see if this will be fixed eventually
                const string suffix = " (loading...)";
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                }
            }
            return name;
        }

        public static string GetDisplayName(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string name = GetCustomUniqueName(envDTEProject);
            if (IsWebSite(envDTEProject))
            {
                name = PathHelper.SmartTruncate(name, 40);
            }
            return name;
        }

        /// <summary>
        /// This method is different from the GetName() method above in that for Website project,
        /// it will always return the project name, instead of the full path to the website, when it uses Casini
        /// server.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We can treat the website as running IISExpress if we can't get the WebSiteType property.")]
        internal static string GetProperName(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsWebSite(envDTEProject))
            {
                try
                {
                    // if the WebSiteType property of a WebSite project, it means the Website is configured to run with Casini, 
                    // as opposed to IISExpress. In which case, Project.Name will return the full path to the directory of the website. 
                    // We want to extract out the directory name only. 
                    object websiteType = envDTEProject.Properties.Item("WebSiteType").Value;
                    if (Convert.ToInt32(websiteType, CultureInfo.InvariantCulture) == 0)
                    {
                        // remove the trailing slash. 
                        string projectPath = envDTEProject.Name;
                        if (projectPath.Length > 0
                            && projectPath[projectPath.Length - 1] == Path.DirectorySeparatorChar)
                        {
                            projectPath = projectPath.Substring(0, projectPath.Length - 1);
                        }

                        // without the trailing slash, a directory looks like a file name. Hence, call GetFileName gives us the directory name.
                        return Path.GetFileName(projectPath);
                    }
                }
                catch (Exception)
                {
                    // ignore this exception if we can't get the WebSiteType property
                }
            }

            return GetName(envDTEProject);
        }

        public static string GetUniqueName(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsWixProject(envDTEProject))
            {
                // Wix project doesn't offer UniqueName property
                return envDTEProject.FullName;
            }

            try
            {
                return envDTEProject.UniqueName;
            }
            catch (COMException)
            {
                return envDTEProject.FullName;
            }
        }

        /// <summary>
        /// Returns the unique name of the specified project including all solution folder names containing it.
        /// </summary>
        /// <remarks>
        /// This is different from the DTE Project.UniqueName property, which is the absolute path to the project file.
        /// </remarks>
        public static string GetCustomUniqueName(EnvDTEProject envDTEProject)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate { return await GetCustomUniqueNameAsync(envDTEProject); });
        }

        public static async Task<string> GetCustomUniqueNameAsync(EnvDTEProject envDTEProject)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (IsWebSite(envDTEProject))
            {
                // website projects always have unique name
                return envDTEProject.Name;
            }
            Stack<string> nameParts = new Stack<string>();

            EnvDTEProject cursor = envDTEProject;
            nameParts.Push(GetName(cursor));

            // walk up till the solution root
            while (cursor.ParentProjectItem != null
                   && cursor.ParentProjectItem.ContainingProject != null)
            {
                cursor = cursor.ParentProjectItem.ContainingProject;
                nameParts.Push(GetName(cursor));
            }

            return String.Join("\\", nameParts);
        }

        internal static bool IsExplicitlyUnsupported(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return envDTEProject.Kind == null || UnsupportedProjectTypes.Contains(envDTEProject.Kind);
        }

        public static bool IsParentProjectExplicitlyUnsupported(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (envDTEProject.ParentProjectItem == null
                || envDTEProject.ParentProjectItem.ContainingProject == null)
            {
                // this project is not a child of another project
                return false;
            }

            EnvDTEProject parentEnvDTEProject = envDTEProject.ParentProjectItem.ContainingProject;
            return IsExplicitlyUnsupported(parentEnvDTEProject);
        }

        /// <summary>
        /// Recursively retrieves all supported child projects of a virtual folder.
        /// </summary>
        /// <param name="project">The root container project</param>
        internal static IEnumerable<EnvDTEProject> GetSupportedChildProjects(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!IsSolutionFolder(envDTEProject))
            {
                yield break;
            }

            var containerProjects = new Queue<EnvDTEProject>();
            containerProjects.Enqueue(envDTEProject);

            while (containerProjects.Any())
            {
                var containerProject = containerProjects.Dequeue();
                foreach (EnvDTEProjectItem item in containerProject.ProjectItems)
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

        internal static MicrosoftBuildEvaluationProject AsMicrosoftBuildEvaluationProject(string dteProjectFullName)
        {
            // Need NOT be on the UI thread

            return ProjectCollection.GlobalProjectCollection.GetLoadedProjects(dteProjectFullName).FirstOrDefault() ??
                   ProjectCollection.GlobalProjectCollection.LoadProject(dteProjectFullName);
        }

        internal static NuGetFramework GetTargetNuGetFramework(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var targetFrameworkMoniker = GetTargetFrameworkString(envDTEProject);

            if (!string.IsNullOrEmpty(targetFrameworkMoniker))
            {
                return NuGetFramework.Parse(targetFrameworkMoniker);
            }

            return NuGetFramework.UnsupportedFramework;
        }

        /// <summary>
        /// Determine the project framework string based on the project properties.
        /// </summary>
        public static string GetTargetFrameworkString(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (envDTEProject == null)
            {
                return null;
            }

            var projectPath = GetFullProjectPath(envDTEProject);
            var platformIdentifier = GetPropertyValue<string>(envDTEProject, "TargetPlatformIdentifier");
            var platformVersion = GetPropertyValue<string>(envDTEProject, "TargetPlatformVersion");
            var platformMinVersion = GetPropertyValue<string>(envDTEProject, "TargetPlatformMinVersion");
            var targetFrameworkMoniker = GetPropertyValue<string>(envDTEProject, "TargetFrameworkMoniker");
            var isManagementPackProject = IsManagementPackProject(envDTEProject);
            var isXnaWindowsPhoneProject = IsXnaWindowsPhoneProject(envDTEProject);

            // Projects supporting TargetFramework and TargetFrameworks are detected before
            // this check. The values can be passed as null here.
            var frameworkStrings = MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                projectFilePath: projectPath,
                targetFrameworks: null,
                targetFramework: null,
                targetFrameworkMoniker: targetFrameworkMoniker,
                targetPlatformIdentifier: platformIdentifier,
                targetPlatformVersion: platformVersion,
                targetPlatformMinVersion: platformMinVersion,
                isManagementPackProject: isManagementPackProject,
                isXnaWindowsPhoneProject: isXnaWindowsPhoneProject);

            return frameworkStrings.FirstOrDefault();
        }

        internal static async Task<bool> ContainsFile(EnvDTEProject envDTEProject, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.Equals(envDTEProject.Kind, NuGetVSConstants.WixProjectTypeGuid, StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(envDTEProject.Kind, NuGetVSConstants.NemerleProjectTypeGuid, StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(envDTEProject.Kind, NuGetVSConstants.FsharpProjectTypeGuid, StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(envDTEProject.Kind, NuGetVSConstants.JsProjectTypeGuid, StringComparison.OrdinalIgnoreCase))
            {
                // For Wix and Nemerle projects, IsDocumentInProject() returns not found
                // even though the file is in the project. So we use GetProjectItem()
                // instead. Nemerle is a high-level statically typed programming language for .NET platform
                // Note that pszMkDocument, the document moniker, passed to IsDocumentInProject(), must be a path to the file
                // for certain file-based project systems such as F#. And, not just a filename. For these project systems as well,
                // do the following
                EnvDTEProjectItem item = await GetProjectItemAsync(envDTEProject, path);
                return item != null;
            }
            IVsProject vsProject = (IVsProject)VsHierarchyUtility.ToVsHierarchy(envDTEProject);
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
                VSDOCUMENTPRIORITY[] priority = new VSDOCUMENTPRIORITY[1];
                int hr = vsProject.IsDocumentInProject(path, out pFound, priority, out itemId);
                return ErrorHandler.Succeeded(hr) && pFound == 1 && priority[0] >= VSDOCUMENTPRIORITY.DP_Standard;
            }

            int hres = vsProject.IsDocumentInProject(path, out pFound, new VSDOCUMENTPRIORITY[0], out itemId);
            return ErrorHandler.Succeeded(hres) && pFound == 1;
        }

        // Get the ProjectItems for a folder path
        public static async Task<EnvDTEProjectItems> GetProjectItemsAsync(EnvDTEProject envDTEProject, string folderPath, bool createIfNotExists)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (String.IsNullOrEmpty(folderPath))
            {
                return envDTEProject.ProjectItems;
            }

            // Traverse the path to get at the directory
            string[] pathParts = folderPath.Split(PathSeparatorChars, StringSplitOptions.RemoveEmptyEntries);

            // 'cursor' can contain a reference to either a Project instance or ProjectItem instance. 
            // Both types have the ProjectItems property that we want to access.
            object cursor = envDTEProject;

            string fullPath = GetFullPath(envDTEProject);
            string folderRelativePath = String.Empty;

            foreach (string part in pathParts)
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
        private static async Task<EnvDTEProjectItem> GetOrCreateFolderAsync(
            EnvDTEProject envDTEProject,
            object parentItem,
            string fullPath,
            string folderRelativePath,
            string folderName,
            bool createIfNotExists)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (parentItem == null)
            {
                return null;
            }

            EnvDTEProjectItem subFolder;

            EnvDTEProjectItems envDTEProjectItems = GetProjectItems(parentItem);
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
                if (IsJavaScriptProject(envDTEProject)
                    && Directory.Exists(fullPath))
                {
                    bool succeeded = await IncludeExistingFolderToProjectAsync(envDTEProject, folderRelativePath);
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

        private static bool TryGetFolder(EnvDTEProjectItems envDTEProjectItems, string name, out EnvDTEProjectItem envDTEProjectItem)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            envDTEProjectItem = GetProjectItem(envDTEProjectItems, name, FolderKinds);

            return envDTEProjectItem != null;
        }

        private static async Task<bool> IncludeExistingFolderToProjectAsync(EnvDTEProject envDTEProject, string folderRelativePath)
        {
            // Execute command to include the existing folder into project. Must do this on UI thread.
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsUIHierarchy projectHierarchy = (IVsUIHierarchy)VsHierarchyUtility.ToVsHierarchy(envDTEProject);

            uint itemId;
            int hr = projectHierarchy.ParseCanonicalName(folderRelativePath, out itemId);
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

        private static bool TryGetFile(EnvDTEProjectItems envDTEProjectItems, string name, out EnvDTEProjectItem envDTEProjectItem)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

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
        private static bool TryGetNestedFile(EnvDTEProjectItems envDTEProjectItems, string name, out EnvDTEProjectItem envDTEProjectItem)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            string parentFileName;
            if (!KnownNestedFiles.TryGetValue(name, out parentFileName))
            {
                parentFileName = Path.GetFileNameWithoutExtension(name);
            }

            // If it's not one of the known nested files then we're going to look up prefixes backwards
            // i.e. if we're looking for foo.aspx.cs then we look for foo.aspx then foo.aspx.cs as a nested file
            EnvDTEProjectItem parentEnvDTEProjectItem = GetProjectItem(envDTEProjectItems, parentFileName, FileKinds);

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
        private static EnvDTEProjectItem GetProjectItem(EnvDTEProjectItems envDTEProjectItems, string name, IEnumerable<string> allowedItemKinds)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            try
            {
                EnvDTEProjectItem envDTEProjectItem = envDTEProjectItems.Item(name);
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

        private static EnvDTEProjectItems GetProjectItems(object parent)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var envDTEProject = parent as EnvDTEProject;
            if (envDTEProject != null)
            {
                return envDTEProject.ProjectItems;
            }

            var envDTEProjectItem = parent as EnvDTEProjectItem;
            if (envDTEProjectItem != null)
            {
                return envDTEProjectItem.ProjectItems;
            }

            return null;
        }

        internal static async Task<EnvDTEProjectItem> GetProjectItemAsync(EnvDTEProject envDTEProject, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string folderPath = Path.GetDirectoryName(path);
            string itemName = Path.GetFileName(path);

            EnvDTEProjectItems container = await GetProjectItemsAsync(envDTEProject, folderPath, createIfNotExists: false);

            EnvDTEProjectItem projectItem;
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

        internal static bool SupportsReferences(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return envDTEProject.Kind != null &&
                   !UnsupportedProjectTypesForAddingReferences.Contains(envDTEProject.Kind, StringComparer.OrdinalIgnoreCase);
        }

        internal static bool SupportsBindingRedirects(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return (envDTEProject.Kind != null & !UnsupportedProjectTypesForBindingRedirects.Contains(envDTEProject.Kind, StringComparer.OrdinalIgnoreCase)) &&
                   !IsWindowsStoreApp(envDTEProject);
        }

        // TODO: Return null for library projects
        internal static string GetConfigurationFile(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return IsWebProject(envDTEProject) ? WebConfig : AppConfig;
        }

        internal static FrameworkName GetDotNetFrameworkName(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string targetFrameworkMoniker = GetTargetFrameworkString(envDTEProject);
            if (!String.IsNullOrEmpty(targetFrameworkMoniker))
            {
                return new FrameworkName(targetFrameworkMoniker);
            }

            return null;
        }

        internal static HashSet<string> GetAssemblyClosure(EnvDTEProject envDTEProject, IDictionary<string, HashSet<string>> visitedProjects)
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

        private static HashSet<string> GetLocalProjectAssemblies(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (IsWebSite(envDTEProject))
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

        private static HashSet<string> GetWebsiteLocalAssemblies(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var assemblies = new HashSet<string>(PathComparer.Default);
            AssemblyReferences references = GetAssemblyReferences(envDTEProject);
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
            string envDTEProjectPath = GetFullPath(envDTEProject);
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

        internal static IList<EnvDTEProject> GetReferencedProjects(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsWebSite(envDTEProject))
            {
                return GetWebsiteReferencedProjects(envDTEProject);
            }

            var envDTEProjects = new List<EnvDTEProject>();
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

        private static IList<EnvDTEProject> GetWebsiteReferencedProjects(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var envDTEProjects = new List<EnvDTEProject>();
            AssemblyReferences references = GetAssemblyReferences(envDTEProject);
            foreach (AssemblyReference reference in references)
            {
                if (reference.ReferencedProject != null)
                {
                    envDTEProjects.Add(reference.ReferencedProject);
                }
            }
            return envDTEProjects;
        }

        internal static async Task<IEnumerable<EnvDTEProjectItem>> GetChildItems(EnvDTEProject envDTEProject, string path, string filter, string desiredKind)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTEProjectItems projectItems = await GetProjectItemsAsync(envDTEProject, path, createIfNotExists: false);

            if (projectItems == null)
            {
                return Enumerable.Empty<EnvDTEProjectItem>();
            }

            Regex matcher = filter.Equals("*.*", StringComparison.OrdinalIgnoreCase) ? null : GetFilterRegex(filter);

            return from EnvDTEProjectItem p in projectItems
                   where desiredKind.Equals(p.Kind, StringComparison.OrdinalIgnoreCase) &&
                         (matcher == null || matcher.IsMatch(p.Name))
                   select p;
        }

        private static Regex GetFilterRegex(string wildcard)
        {
            // Need NOT be on the UI thread

            string pattern = String.Join(String.Empty, wildcard.Split('.').Select(GetPattern));
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        }

        private static string GetPattern(string token)
        {
            // Need NOT be on the UI thread

            return token == "*" ? @"(.*)" : @"(" + token + ")";
        }

        /// <summary>
        /// A DTE specific helper method that validates a path to ensure that it 
        /// could be for a file as opposed to a URL or other invalid path, and
        /// not for a directory. This is used to help determine if a value returned
        /// from DTE is a directory or file, since the file may still be in 
        /// memory and not yet written to disk File.Exists will not work.
        /// </summary>
        private static string GetPotentialFullPathOrNull(string path)
        {
            string fullPath = null;

            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    // Attempt to get the full path
                    fullPath = Path.GetFullPath(path);

                    // Some project systems may return a directory for the file path. 
                    // Directories usually exist even when the in-memory files have not yet 
                    // been written, so we can try to detect obvious non-files here.
                    // WebSites and Win JS projects can return a directory instead of the project file path.
                    if (Directory.Exists(fullPath))
                    {
                        // Ignore directories
                        fullPath = null;
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException
                || ex is NotSupportedException
                || ex is PathTooLongException
                || ex is SecurityException)
            {
                // Ignore invalid paths
                // This can occur if the path was a URL
            }

            return fullPath;
        }

        #endregion // Get "Project" Information

        #region Check Project Types

        private static bool IsJavaScriptProject(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            return envDTEProject != null && NuGetVSConstants.JsProjectTypeGuid.Equals(envDTEProject.Kind, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsManagementPackProject(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            return envDTEProject != null && NuGetVSConstants.ManagementPackProjectTypeGuid.Equals(envDTEProject.Kind, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsXnaWindowsPhoneProject(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            // XNA projects will have this property set
            const string xnaPropertyValue = "Microsoft.Xna.GameStudio.CodeProject.WindowsPhoneProjectPropertiesExtender.XnaRefreshLevel";
            return envDTEProject != null &&
                   "Windows Phone OS 7.1".Equals(GetPropertyValue<string>(envDTEProject, xnaPropertyValue), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNativeProject(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            return envDTEProject != null
                && NuGetVSConstants.CppProjectTypeGuid.Equals(envDTEProject.Kind, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWebProject(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            string[] types = VsHierarchyUtility.GetProjectTypeGuids(envDTEProject);
            return types.Contains(NuGetVSConstants.WebSiteProjectTypeGuid, StringComparer.OrdinalIgnoreCase) ||
                   types.Contains(NuGetVSConstants.WebApplicationProjectTypeGuid, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsWebSite(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return envDTEProject.Kind != null && envDTEProject.Kind.Equals(NuGetVSConstants.WebSiteProjectTypeGuid, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWindowsStoreApp(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            string[] types = VsHierarchyUtility.GetProjectTypeGuids(envDTEProject);
            return types.Contains(NuGetVSConstants.WindowsStoreProjectTypeGuid, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsWixProject(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            return envDTEProject.Kind != null && envDTEProject.Kind.Equals(NuGetVSConstants.WixProjectTypeGuid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if the project has an unsupported project capability, such as, "SharedAssetsProject"
        /// </summary>
        public static bool HasUnsupportedProjectCapability(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var hier = VsHierarchyUtility.ToVsHierarchy(envDTEProject);

            foreach (var unsupportedProjectCapability in UnsupportedProjectCapabilities)
            {
                if (hier.IsCapabilityMatch(unsupportedProjectCapability))
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task<bool> IsBuildIntegrated(EnvDTEProject envDTEProject)
        {
            return (await HasBuildIntegratedConfig(envDTEProject) || SupportsINuGetProjectSystem(envDTEProject));
        }

        public static bool SupportsINuGetProjectSystem(EnvDTEProject envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectKProject = ProjectKNuGetProjectProvider.GetProjectKProject(envDTEProject);
            return projectKProject != null;
        }

        /// <summary>
        /// True if the project has a project.json file, indicating that it is build integrated
        /// </summary>
        public static async Task<bool> HasBuildIntegratedConfig(EnvDTEProject project)
        {
            var projectNameConfig = ProjectJsonPathUtilities.GetProjectConfigWithProjectName(project.Name);

            var containsProjectJson = await ContainsFile(project, projectNameConfig);

            var containsProjectNameJson = await ContainsFile(
                project,
                ProjectJsonPathUtilities.ProjectConfigFileName);

            return containsProjectJson || containsProjectNameJson;
        }

        #endregion // Check Project Types

        #region Act on Project

        public static IVsProjectBuildSystem GetVsProjectBuildSystem(EnvDTEProject project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Get the vs solution
                IVsSolution solution = ServiceLocator.GetInstance<IVsSolution>();
                IVsHierarchy hierarchy;
                var hr = solution.GetProjectOfUniqueName(GetUniqueName(project), out hierarchy);

                if (hr != NuGetVSConstants.S_OK)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                return hierarchy as IVsProjectBuildSystem;
            });
        }

        internal static void EnsureCheckedOutIfExists(EnvDTEProject envDTEProject, string root, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var fullPath = FileSystemUtility.GetFullPath(root, path);
            var dte = envDTEProject.DTE;
            if (dte != null)
            {
                DTESourceControlUtility.EnsureCheckedOutIfExists(dte.SourceControl, fullPath);
            }
        }

        internal static async Task<bool> DeleteProjectItemAsync(EnvDTEProject envDTEProject, string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTEProjectItem projectItem = await GetProjectItemAsync(envDTEProject, path);
            if (projectItem == null)
            {
                return false;
            }

            projectItem.Delete();
            return true;
        }

        internal static void AddImportStatement(EnvDTEProject project, string targetsPath, ImportLocation location)
        {
            // Need NOT be on the UI Thread
            MicrosoftBuildEvaluationProjectUtility.AddImportStatement(AsMSBuildProject(project), targetsPath, location);
        }

        internal static void RemoveImportStatement(EnvDTEProject project, string targetsPath)
        {
            // Need NOT be on the UI Thread
            MicrosoftBuildEvaluationProjectUtility.RemoveImportStatement(AsMSBuildProject(project), targetsPath);
        }

        private static MicrosoftBuildEvaluationProject AsMSBuildProject(EnvDTEProject project)
        {
            // Need NOT be on the UI Thread
            return ProjectCollection.GlobalProjectCollection.GetLoadedProjects(project.FullName).FirstOrDefault() ??
                   ProjectCollection.GlobalProjectCollection.LoadProject(project.FullName);
        }

        internal static void Save(EnvDTEProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                FileSystemUtility.MakeWritable(project.FullName);
                project.Save();
            }
            catch (Exception ex)
            {
                ExceptionHelper.WriteToActivityLog(ex);
            }
        }

        #endregion
    }
}
