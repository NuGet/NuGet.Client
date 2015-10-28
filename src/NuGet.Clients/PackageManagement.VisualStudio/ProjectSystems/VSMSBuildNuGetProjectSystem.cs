// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProjectItems = EnvDTE.ProjectItems;
using EnvDTEProperty = EnvDTE.Property;
using Constants = NuGet.ProjectManagement.Constants;
using MicrosoftBuildEvaluationProject = Microsoft.Build.Evaluation.Project;
using MicrosoftBuildEvaluationProjectItem = Microsoft.Build.Evaluation.ProjectItem;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSMSBuildNuGetProjectSystem : IMSBuildNuGetProjectSystem
    {
        private const string BinDir = "bin";
        private const string NuGetImportStamp = "NuGetPackageImportStamp";
        private IVsProjectBuildSystem _buildSystem;
        private bool _buildSystemFetched;

        public VSMSBuildNuGetProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
        {
            if (envDTEProject == null)
            {
                throw new ArgumentNullException("envDTEProject");
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            EnvDTEProject = envDTEProject;
            NuGetProjectContext = nuGetProjectContext;
        }

        public EnvDTEProject EnvDTEProject { get; }

        public INuGetProjectContext NuGetProjectContext { get; private set; }

        private IScriptExecutor _scriptExecutor;

        private IScriptExecutor ScriptExecutor
        {
            get
            {
                if (_scriptExecutor == null)
                {
                    _scriptExecutor = ServiceLocator.GetInstanceSafe<IScriptExecutor>();
                }

                return _scriptExecutor;
            }
        }

        public IVsProjectBuildSystem ProjectBuildSystem
        {
            get
            {
                if (!_buildSystemFetched)
                {
                    _buildSystem = EnvDTEProjectUtility.GetVsProjectBuildSystem(EnvDTEProject);
                    _buildSystemFetched = true;
                }

                return _buildSystem;
            }
        }

        private string _projectFullPath;

        /// <summary>
        /// This does not contain the filename, just the path to the directory where the project file exists
        /// </summary>
        public string ProjectFullPath
        {
            get
            {
                if (String.IsNullOrEmpty(_projectFullPath))
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _projectFullPath = EnvDTEProjectUtility.GetFullPath(EnvDTEProject);
                    });
                }

                return _projectFullPath;
            }
        }

        private string _projectName;

        public virtual string ProjectName
        {
            get
            {
                if (String.IsNullOrEmpty(_projectName))
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _projectName = EnvDTEProject.Name;
                    });
                }
                return _projectName;
            }
        }

        private string _projectCustomUniqueName;

        public virtual string ProjectUniqueName
        {
            get
            {
                if (String.IsNullOrEmpty(_projectCustomUniqueName))
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            _projectCustomUniqueName = EnvDTEProjectUtility.GetCustomUniqueName(EnvDTEProject);
                        });
                }

                return _projectCustomUniqueName;
            }
        }

        private NuGetFramework _targetFramework;

        public NuGetFramework TargetFramework
        {
            get
            {
                if (_targetFramework == null)
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            _targetFramework = EnvDTEProjectUtility.GetTargetNuGetFramework(EnvDTEProject) ?? NuGetFramework.UnsupportedFramework;
                        });
                }

                return _targetFramework;
            }
        }

        public void SetNuGetProjectContext(INuGetProjectContext nuGetProjectContext)
        {
            NuGetProjectContext = nuGetProjectContext;
        }

        public virtual void AddFile(string path, Stream stream)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await AddFileCoreAsync(path, () => FileSystemUtility.AddFile(ProjectFullPath, path, stream, NuGetProjectContext));
            });
        }

        public virtual void AddFile(string path, Action<Stream> writeToStream)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await AddFileCoreAsync(path, () => FileSystemUtility.AddFile(ProjectFullPath, path, writeToStream, NuGetProjectContext));
            });
        }

        private Task AddFileCoreAsync(string path, Action addFile)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            // Do not try to add file to project, if the path is null or empty.
            if (string.IsNullOrEmpty(path))
            {
                return Task.FromResult(false);
            }

            bool fileExistsInProject = FileExistsInProject(path);

            // If the file exists on disk but not in the project then skip it.
            // One exception is the 'packages.config' file, in which case we want to include
            // it into the project.
            // Other exceptions are 'web.config' and 'app.config'
            var fileName = Path.GetFileName(path);
            if (File.Exists(Path.Combine(ProjectFullPath, path))
                && !fileExistsInProject
                && !fileName.Equals(Constants.PackageReferenceFile)
                && !fileName.Equals("packages." + ProjectName + ".config")
                && !fileName.Equals(EnvDTEProjectUtility.WebConfig)
                && !fileName.Equals(EnvDTEProjectUtility.AppConfig))
            {
                NuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, Strings.Warning_FileAlreadyExists, path);
            }
            else
            {
                EnvDTEProjectUtility.EnsureCheckedOutIfExists(EnvDTEProject, ProjectFullPath, path);
                addFile();
                if (!fileExistsInProject)
                {
                    return AddFileToProjectAsync(path);
                }
            }

            return Task.FromResult(false);
        }

        public void AddExistingFile(string path)
        {
            var fullPath = Path.Combine(ProjectFullPath, path);

            if (!File.Exists(fullPath))
            {
                throw new ArgumentNullException(String.Format(CultureInfo.CurrentCulture, Strings.PathToExistingFileNotPresent, fullPath, ProjectName));
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await AddFileCoreAsync(path, () => { });
            });
        }

        protected virtual bool ExcludeFile(string path)
        {
            // Exclude files from the bin directory.
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            return Path.GetDirectoryName(path).Equals(BinDir, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// This method should be on the UI thread. The overrides should ensure that
        /// </summary>
        protected virtual async Task AddFileToProjectAsync(string path)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (ExcludeFile(path))
            {
                return;
            }

            // Get the project items for the folder path
            string folderPath = Path.GetDirectoryName(path);
            string fullPath = FileSystemUtility.GetFullPath(ProjectFullPath, path);

            var container = await EnvDTEProjectUtility.GetProjectItemsAsync(EnvDTEProject, folderPath, createIfNotExists: true);

            // Add the file to project or folder
            AddFileToContainer(fullPath, folderPath, container);

            NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_AddedFileToProject, path, ProjectName);
        }

        /// <summary>
        /// This method should be on the UI thread. The overrides should ensure that
        /// </summary>
        protected virtual void AddFileToContainer(string fullPath, string folderPath, EnvDTEProjectItems container)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            container.AddFromFileCopy(fullPath);
        }

        public void AddFrameworkReference(string name)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    // Add a reference to the project
                    AddGacReference(name);

                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_AddGacReference, name, ProjectName);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Strings.FailedToAddGacReference, name), e);
                }
            });
        }

        /// <summary>
        /// This method should be on the UI thread. The overrides should ensure that
        /// </summary>
        protected virtual void AddGacReference(string name)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            EnvDTEProjectUtility.GetReferences(EnvDTEProject).Add(name);
        }

        public virtual void AddImport(string targetFullPath, ImportLocation location)
        {
            if (String.IsNullOrEmpty(targetFullPath))
            {
                throw new ArgumentNullException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "targetPath");
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                string relativeTargetPath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(ProjectFullPath), targetFullPath);
                EnvDTEProjectUtility.AddImportStatement(EnvDTEProject, relativeTargetPath, location);
                EnvDTEProjectUtility.Save(EnvDTEProject);

                // notify the project system of the change
                UpdateImportStamp(EnvDTEProject);
            });
        }

        public virtual void AddReference(string referencePath)
        {
            if (referencePath == null)
            {
                throw new ArgumentNullException("referencePath");
            }

            string name = Path.GetFileNameWithoutExtension(referencePath);

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    // Get the full path to the reference
                    string fullPath = Path.Combine(ProjectFullPath, referencePath);

                    // Add a reference to the project
                    var references = EnvDTEProjectUtility.GetReferences(EnvDTEProject);

                    dynamic reference = references.Add(fullPath);

                    if (reference != null)
                    {
                        var path = GetReferencePath(reference);

                        // If path != fullPath, we need to set CopyLocal thru msbuild by setting Private
                        // to true.
                        // This happens if the assembly appears in any of the search paths that VS uses to
                        // locate assembly references.
                        // Most commonly, it happens if this assembly is in the GAC or in the output path.
                        if (path != null
                            && !path.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            // Get the msbuild project for this project
                            MicrosoftBuildEvaluationProject buildProject = EnvDTEProjectUtility.AsMicrosoftBuildEvaluationProject(EnvDTEProject);

                            if (buildProject != null)
                            {
                                // Get the assembly name of the reference we are trying to add
                                AssemblyName assemblyName = AssemblyName.GetAssemblyName(fullPath);

                                // Try to find the item for the assembly name
                                MicrosoftBuildEvaluationProjectItem item = (from assemblyReferenceNode in buildProject.GetAssemblyReferences()
                                    where AssemblyNamesMatch(assemblyName, assemblyReferenceNode.Item2)
                                    select assemblyReferenceNode.Item1).FirstOrDefault();

                                if (item != null)
                                {
                                    // Add the <HintPath> metadata item as a relative path
                                    string projectPath = PathUtility.EnsureTrailingSlash(ProjectFullPath);
                                    string relativePath = PathUtility.GetRelativePath(projectPath, referencePath);

                                    item.SetMetadataValue("HintPath", relativePath);

                                    // Set <Private> to true
                                    item.SetMetadataValue("Private", "True");

                                    // Save the project after we've modified it.
                                    FileSystemUtility.MakeWritable(EnvDTEProject.FullName);
                                    EnvDTEProject.Save();
                                }
                            }
                        }
                        else
                        {
                            TrySetCopyLocal(reference);
                            TrySetSpecificVersion(reference);
                        }
                    }

                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_AddReference, name, ProjectName);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Strings.FailedToAddReference, name), e);
                }
            });
        }

        private static bool AssemblyNamesMatch(AssemblyName name1, AssemblyName name2)
        {
            return name1.Name.Equals(name2.Name, StringComparison.OrdinalIgnoreCase) &&
                   EqualsIfNotNull(name1.Version, name2.Version) &&
                   EqualsIfNotNull(name1.CultureInfo, name2.CultureInfo) &&
                   EqualsIfNotNull(name1.GetPublicKeyToken(), name2.GetPublicKeyToken(), Enumerable.SequenceEqual);
        }

        private static bool EqualsIfNotNull<T>(T obj1, T obj2)
        {
            return EqualsIfNotNull(obj1, obj2, (a, b) => a.Equals(b));
        }

        private static bool EqualsIfNotNull<T>(T obj1, T obj2, Func<T, T, bool> equals)
        {
            // If both objects are non null do the equals
            if (obj1 != null
                && obj2 != null)
            {
                return equals(obj1, obj2);
            }

            // Otherwise consider them equal if either of the values are null
            return true;
        }

        public virtual void RemoveFile(string path)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var deleteProjectItem = await EnvDTEProjectUtility.DeleteProjectItemAsync(EnvDTEProject, path);
                if (deleteProjectItem)
                {
                    string folderPath = Path.GetDirectoryName(path);
                    if (!String.IsNullOrEmpty(folderPath))
                    {
                        NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_RemovedFileFromFolder, Path.GetFileName(path), folderPath);
                    }
                    else
                    {
                        NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_RemovedFile, Path.GetFileName(path));
                    }
                }
            });
        }

        public virtual bool ReferenceExists(string name)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    string referenceName = name;
                    if (Constants.AssemblyReferencesExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
                    {
                        // Get the reference name without extension
                        referenceName = Path.GetFileNameWithoutExtension(name);
                    }

                    return EnvDTEProjectUtility.GetReferences(EnvDTEProject).Item(referenceName) != null;
                }
                catch
                {
                }
                return false;
            });
        }

        public virtual void RemoveImport(string targetFullPath)
        {
            if (String.IsNullOrEmpty(targetFullPath))
            {
                throw new ArgumentNullException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "targetPath");
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                string relativeTargetPath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(ProjectFullPath), targetFullPath);
                EnvDTEProjectUtility.RemoveImportStatement(EnvDTEProject, relativeTargetPath);

                EnvDTEProjectUtility.Save(EnvDTEProject);

                // notify the project system of the change
                UpdateImportStamp(EnvDTEProject);
            });
        }

        public virtual void RemoveReference(string name)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    // Get the reference name without extension
                    string referenceName = Path.GetFileNameWithoutExtension(name);

                    // Remove the reference from the project
                    // NOTE:- Project.Object.References.Item requires Reference.Identity
                    //        which is, the Assembly name without path or extension
                    //        But, we pass in the assembly file name. And, this works for
                    //        almost all the assemblies since Assembly Name is the same as the assembly file name
                    //        In case of F#, the input parameter is case-sensitive as well
                    //        Hence, an override to THIS function is added to take care of that
                    var reference = EnvDTEProjectUtility.GetReferences(EnvDTEProject).Item(referenceName);
                    if (reference != null)
                    {
                        reference.Remove();
                        NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_RemoveReference, name, ProjectName);
                    }
                }
                catch (Exception e)
                {
                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, e.Message);
                }
            });
        }

        private static void TrySetCopyLocal(dynamic reference)
        {
            // Always set copy local to true for references that we add
            try
            {
                // In order to properly write this to MSBuild in ALL cases, we have to trigger the Property Change
                // notification with a new value of "true". However, "true" is the default value, so in order to
                // cause a notification to fire, we have to set it to false and then back to true
                reference.CopyLocal = false;
                reference.CopyLocal = true;
            }
            catch (NotSupportedException)
            {
            }
            catch (NotImplementedException)
            {
            }
            catch (RuntimeBinderException)
            {
            }
            catch (COMException)
            {
            }
        }

        private static string GetReferencePath(dynamic reference)
        {
            try
            {
                return reference.Path;
            }
            catch (NotSupportedException)
            {
            }
            catch (NotImplementedException)
            {
            }
            catch (RuntimeBinderException)
            {
            }
            catch (COMException)
            {
            }

            return null;
        }

        // Set SpecificVersion to true
        private static void TrySetSpecificVersion(dynamic reference)
        {
            // Always set SpecificVersion to true for references that we add
            try
            {
                reference.SpecificVersion = false;
                reference.SpecificVersion = true;
            }
            catch (NotSupportedException)
            {
            }
            catch (NotImplementedException)
            {
            }
            catch (RuntimeBinderException)
            {
            }
            catch (COMException)
            {
            }
        }

        public virtual bool FileExistsInProject(string path)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var containsFile = await EnvDTEProjectUtility.ContainsFile(EnvDTEProject, path);
                    return containsFile;
                });
        }

        public virtual dynamic GetPropertyValue(string propertyName)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    EnvDTEProperty envDTEProperty = EnvDTEProject.Properties.Item(propertyName);
                    if (envDTEProperty != null)
                    {
                        return envDTEProperty.Value;
                    }
                }
                catch (ArgumentException)
                {
                    // If the property doesn't exist this will throw an argument exception
                }
                return null;
            });
        }

        public virtual bool IsSupportedFile(string path)
        {
            // Need NOT be on the UI thread

            string fileName = Path.GetFileName(path);

            // exclude all file names with the pattern as "web.*.config",
            // e.g. web.config, web.release.config, web.debug.config
            return !(fileName.StartsWith("web.", StringComparison.OrdinalIgnoreCase) &&
                     fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase));
        }

        public virtual string ResolvePath(string path)
        {
            return path;
        }

        /// <summary>
        /// This method should be on the UI thread. The overrides should ensure that
        /// Sets NuGetPackageImportStamp to a new random guid. This is a hack to let the project system know it is out
        /// of date.
        /// The value does not matter, it just needs to change.
        /// </summary>
        protected static void UpdateImportStamp(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            IVsBuildPropertyStorage propStore = VsHierarchyUtility.ToVsHierarchy(envDTEProject) as IVsBuildPropertyStorage;
            if (propStore != null)
            {
                // <NuGetPackageImportStamp>af617720</NuGetPackageImportStamp>
                string stamp = Guid.NewGuid().ToString().Split('-')[0];
                try
                {
                    propStore.SetPropertyValue(NuGetImportStamp, string.Empty, (uint)_PersistStorageType.PST_PROJECT_FILE, stamp);
                }
                catch (Exception ex1)
                {
                    ExceptionHelper.WriteToActivityLog(ex1);
                }

                // Remove the NuGetImportStamp so that VC++ project file won't be updated with this stamp on disk,
                // which causes unnecessary source control pending changes.
                try
                {
                    propStore.RemoveProperty(NuGetImportStamp, string.Empty, (uint)_PersistStorageType.PST_PROJECT_FILE);
                }
                catch (Exception ex2)
                {
                    ExceptionHelper.WriteToActivityLog(ex2);
                }
            }
        }

        #region Binding Redirects Stuff

        private const string SilverlightTargetFrameworkIdentifier = "Silverlight";

        protected virtual bool IsBindingRedirectSupported
        {
            get
            {
                // Silverlight projects and Windows Phone projects do not support binding redirect.
                // They both share the same identifier as "Silverlight"
                return !SilverlightTargetFrameworkIdentifier.Equals(TargetFramework.DotNetFrameworkName, StringComparison.OrdinalIgnoreCase);
            }
        }

        public void AddBindingRedirects()
        {
            var settings = ServiceLocator.GetInstanceSafe<Configuration.ISettings>();

            var behavior = new BindingRedirectBehavior(settings);

            if (!behavior.IsSkipped)
            {
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    try
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        InitForBindingRedirects();
                        if (IsBindingRedirectSupported && VSSolutionManager != null)
                        {
                            await RuntimeHelpers.AddBindingRedirectsAsync(VSSolutionManager,
                                EnvDTEProject,
                                VSFrameworkMultiTargeting,
                                NuGetProjectContext);
                        }
                    }
                    catch (Exception ex)
                    {
                        var fileName = EnvDTEProjectUtility.GetFullPath(EnvDTEProject);

                        var level = behavior.FailOperations ?
                            ProjectManagement.MessageLevel.Error :
                            ProjectManagement.MessageLevel.Warning;

                        NuGetProjectContext.Log(level,
                            Strings.FailedToUpdateBindingRedirects,
                            fileName,
                            ex.Message);

                        if (behavior.FailOperations)
                        {
                            throw;
                        }
                    }
                });
            }
        }

        private readonly bool BindingRedirectsRelatedInitialized = false;
        private VSSolutionManager VSSolutionManager { get; set; }
        private IVsFrameworkMultiTargeting VSFrameworkMultiTargeting { get; set; }

        private void InitForBindingRedirects()
        {
            if (!BindingRedirectsRelatedInitialized)
            {
                var solutionManager = ServiceLocator.GetInstanceSafe<ISolutionManager>();
                VSSolutionManager = (solutionManager != null) ? (solutionManager as VSSolutionManager) : null;
                VSFrameworkMultiTargeting = ServiceLocator.GetGlobalService<SVsFrameworkMultiTargeting, IVsFrameworkMultiTargeting>();
            }
        }

        #endregion

        public Task ExecuteScriptAsync(PackageIdentity identity, string packageInstallPath, string scriptRelativePath, NuGetProject nuGetProject, bool throwOnFailure)
        {
            if (ScriptExecutor != null)
            {
                return ScriptExecutor.ExecuteAsync(identity, packageInstallPath, scriptRelativePath, EnvDTEProject, nuGetProject, NuGetProjectContext, throwOnFailure);
            }
            return Task.FromResult(false);
        }

        public virtual void BeginProcessing()
        {
            ProjectBuildSystem?.StartBatchEdit();
        }

        public virtual void RegisterProcessedFiles(IEnumerable<string> files)
        {
            // No-op, this is implemented in other project systems, like website.
        }

        public virtual void EndProcessing()
        {
            ProjectBuildSystem?.EndBatchEdit();
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            // Only delete this folder if it is empty and we didn't specify that we want to recurse
            if (!recursive
                && (FileSystemUtility.GetFiles(ProjectFullPath, path, "*.*", recursive).Any() || FileSystemUtility.GetDirectories(ProjectFullPath, path).Any()))
            {
                NuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, ProjectManagement.Strings.Warning_DirectoryNotEmpty, path);
                return;
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Workaround for TFS update issue. If we're bound to TFS, do not try and delete directories.
                if (SourceControlUtility.GetSourceControlManager(NuGetProjectContext) == null)
                {
                    var deletedProjectItem = await EnvDTEProjectUtility.DeleteProjectItemAsync(EnvDTEProject, path);
                    if (deletedProjectItem)
                    {
                        NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, ProjectManagement.Strings.Debug_RemovedFolder, path);
                    }
                }
            });
        }

        public IEnumerable<string> GetFiles(string path, string filter, bool recursive)
        {
            if (recursive)
            {
                throw new NotSupportedException();
            }

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var childItems = await EnvDTEProjectUtility.GetChildItems(EnvDTEProject, path, filter, NuGetVSConstants.VsProjectItemKindPhysicalFile);
                // Get all physical files
                return from p in childItems
                    select p.Name;
            });
        }

        /// <summary>
        /// Returns the list of full paths of all files in the project whose name is
        /// <paramref name="fileName"/>. BFS algorithm is used. Thus, the files directly under
        /// the project are returned first. Then files one-level deep are returned, and so-on.
        /// </summary>
        /// <param name="fileName">The file name to search.</param>
        /// <returns>The list of full paths.</returns>
        public IEnumerable<string> GetFullPaths(string fileName)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var paths = new List<string>();
                var projectItemsQueue = new Queue<EnvDTEProjectItems>();
                projectItemsQueue.Enqueue(EnvDTEProject.ProjectItems);
                while (projectItemsQueue.Count > 0)
                {
                    var items = projectItemsQueue.Dequeue();
                    foreach (EnvDTE.ProjectItem item in items)
                    {
                        if (item.Kind == NuGetVSConstants.VsProjectItemKindPhysicalFile)
                        {
                            if (StringComparer.OrdinalIgnoreCase.Equals(item.Name, fileName))
                            {
                                paths.Add(item.FileNames[1]);
                            }
                        }
                        else if (item.Kind == NuGetVSConstants.VsProjectItemKindPhysicalFolder)
                        {
                            projectItemsQueue.Enqueue(item.ProjectItems);
                        }
                    }
                }

                return paths;
            });
        }

        public virtual IEnumerable<string> GetDirectories(string path)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var childItems = await EnvDTEProjectUtility.GetChildItems(EnvDTEProject, path, "*.*", NuGetVSConstants.VsProjectItemKindPhysicalFolder);
                // Get all physical folders
                return from p in childItems
                    select p.Name;
            });
        }
    }
}
