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
using NuGet.PackageManagement.UI;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
#if !VS14
using VSLangProj150;
#endif
using Constants = NuGet.ProjectManagement.Constants;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProjectItems = EnvDTE.ProjectItems;
using EnvDTEProperty = EnvDTE.Property;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSMSBuildNuGetProjectSystem : IMSBuildNuGetProjectSystem
    {
        private const string BinDir = "bin";
        private const string NuGetImportStamp = "NuGetPackageImportStamp";
        private IVsProjectBuildSystem _buildSystem;

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
                if (_buildSystem == null)
                {
                    _buildSystem = EnvDTEProjectUtility.GetVsProjectBuildSystem(EnvDTEProject);
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
                if (string.IsNullOrEmpty(_projectFullPath))
                {
                    NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _projectFullPath = EnvDTEProjectUtility.GetFullPath(EnvDTEProject);
                    });
                }

                return _projectFullPath;
            }
        }

        private string _projectFileFullPath;

        /// <summary>
        /// This contains the directory and the file name of the project file.
        /// </summary>
        public string ProjectFileFullPath
        {
            get
            {
                if (string.IsNullOrEmpty(_projectFileFullPath))
                {
                    NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        _projectFileFullPath = EnvDTEProjectUtility.GetFullProjectPath(EnvDTEProject);
                    });
                }

                return _projectFileFullPath;
            }
        }

        private string _projectName;

        public virtual string ProjectName
        {
            get
            {
                if (String.IsNullOrEmpty(_projectName))
                {
                    NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
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
                    NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                        {
                            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
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
                    NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                        {
                            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            _targetFramework = EnvDTEProjectUtility.GetTargetNuGetFramework(EnvDTEProject);
                        });
                }

                return _targetFramework;
            }
        }

        public dynamic VSProject4
        {
            get
            {
#if VS14
                // VSProject4 doesn't apply for Dev14 so simply returns null.
                return null;
#else
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return EnvDTEProject.Object as VSProject4;
                });
#endif
            }
        }

        public void SetNuGetProjectContext(INuGetProjectContext nuGetProjectContext)
        {
            NuGetProjectContext = nuGetProjectContext;
        }

        public virtual void AddFile(string path, Stream stream)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await AddFileCoreAsync(path, () => FileSystemUtility.AddFile(ProjectFullPath, path, stream, NuGetProjectContext));
            });
        }

        public virtual void AddFile(string path, Action<Stream> writeToStream)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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

        public void AddFrameworkReference(string name, string packageId)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    AddGacReference(name);

                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_AddGacReference, name, ProjectName);
                }
                catch (Exception e)
                {
                    if (IsReferenceUnavailableException(e))
                    {
                        var frameworkName = EnvDTEProjectUtility.GetDotNetFrameworkName(EnvDTEProject);

                        if (FrameworkAssemblyResolver.IsFrameworkFacade(name, frameworkName))
                        {
                            NuGetProjectContext.Log(ProjectManagement.MessageLevel.Info, Strings.FailedToAddFacadeReference, name, packageId);
                            return;
                        }
                    }

                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Strings.FailedToAddGacReference, packageId, name), e);
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

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
                throw new ArgumentNullException(nameof(referencePath));
            }

            var name = Path.GetFileNameWithoutExtension(referencePath);
            var projectName = string.Empty;
            var projectFullPath = string.Empty;
            var assemblyFullPath = string.Empty;
            var dteProjectFullName = string.Empty;
            var dteOriginalPath = string.Empty;

            var resolvedToPackage = false;

            try
            {
                // Perform all DTE operations on the UI thread
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Read DTE properties from the UI thread
                    projectFullPath = ProjectFullPath;
                    projectName = ProjectName;
                    dteProjectFullName = EnvDTEProject.FullName;

                    // Get the full path to the reference
                    assemblyFullPath = Path.Combine(projectFullPath, referencePath);

                    // Add a reference to the project
                    var references = EnvDTEProjectUtility.GetReferences(EnvDTEProject);

                    dynamic reference = references.Add(assemblyFullPath);

                    if (reference != null)
                    {
                        dteOriginalPath = GetReferencePath(reference);

                        // If path != fullPath, we need to set CopyLocal thru msbuild by setting Private
                        // to true.
                        // This happens if the assembly appears in any of the search paths that VS uses to
                        // locate assembly references.
                        // Most commonly, it happens if this assembly is in the GAC or in the output path.
                        // The path may be null or for some project system it can be "".
                        resolvedToPackage = !string.IsNullOrWhiteSpace(dteOriginalPath) && IsSamePath(dteOriginalPath, assemblyFullPath);

                        if (resolvedToPackage)
                        {
                            // Set reference properties (if needed)
                            TrySetCopyLocal(reference);
                            TrySetSpecificVersion(reference);
                        }
                    }

                    if (!resolvedToPackage)
                    {
                        // This should be done off the UI thread

                        // Get the msbuild project for this project
                        var buildProject = EnvDTEProjectUtility.AsMicrosoftBuildEvaluationProject(dteProjectFullName);

                        if (buildProject != null)
                        {
                            // Get the assembly name of the reference we are trying to add
                            var assemblyName = AssemblyName.GetAssemblyName(assemblyFullPath);

                            // Try to find the item for the assembly name
                            var item = (from assemblyReferenceNode in buildProject.GetAssemblyReferences()
                                        where AssemblyNamesMatch(assemblyName, assemblyReferenceNode.Item2)
                                        select assemblyReferenceNode.Item1).FirstOrDefault();

                            if (item != null)
                            {
                                // Add the <HintPath> metadata item as a relative path
                                var projectPath = PathUtility.EnsureTrailingSlash(projectFullPath);
                                var relativePath = PathUtility.GetRelativePath(projectPath, referencePath);

                                item.SetMetadataValue("HintPath", relativePath);

                                // Set <Private> to true
                                item.SetMetadataValue("Private", "True");

                                FileSystemUtility.MakeWritable(dteProjectFullName);

                                // Save the project after we've modified it.
                                EnvDTEProject.Save();
                            }
                        }
                        else
                        {
                            // The reference cannot be changed by modifying the project file.
                            // This could be a failure, however that could be a breaking
                            // change if there is a non-msbuild project system relying on this
                            // to skip references.
                            // Log a warning to let the user know that their reference may have failed.
                            NuGetProjectContext.Log(
                                ProjectManagement.MessageLevel.Warning,
                                Strings.FailedToAddReference,
                                name);
                        }
                    }
                });
                
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture, Strings.FailedToAddReference, name), e);
            }

            NuGetProjectContext.Log(
                ProjectManagement.MessageLevel.Debug,
                $"Added reference '{name}' to project:'{projectName}'. Was the Reference Resolved To Package (resolvedToPackage):'{resolvedToPackage}', " +
                "where Reference Path from DTE(dteOriginalPath):'{dteOriginalPath}' and Reference Path from package reference(assemblyFullPath):'{assemblyFullPath}'.");
        }

        private static bool IsSamePath(string path1, string path2)
        {
            // Exact match or match after normalizing both paths
            return StringComparer.OrdinalIgnoreCase.Equals(path1, path2)
                || StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2));
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
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                string relativeTargetPath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(ProjectFullPath), targetFullPath);
                EnvDTEProjectUtility.RemoveImportStatement(EnvDTEProject, relativeTargetPath);

                EnvDTEProjectUtility.Save(EnvDTEProject);

                // notify the project system of the change
                UpdateImportStamp(EnvDTEProject);
            });
        }

        public virtual void RemoveReference(string name)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
                // Setting copyLocal to "true" only if it is "false".
                // This should trigger an event which will result in successful writing to msbuild.
                if (!reference.CopyLocal)
                {
                    reference.CopyLocal = true;
                }
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
                // Setting SpecificVersion to "true" only if it is "false".
                // This should trigger an event which will result in successful writing to msbuild.
                if (!reference.SpecificVersion)
                {
                    reference.SpecificVersion = true;
                }
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
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var containsFile = await EnvDTEProjectUtility.ContainsFile(EnvDTEProject, path);
                    return containsFile;
                });
        }

        public virtual dynamic GetPropertyValue(string propertyName)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
                return !SilverlightTargetFrameworkIdentifier.Equals(TargetFramework.Framework, StringComparison.OrdinalIgnoreCase);
            }
        }

        public void AddBindingRedirects()
        {
            var settings = ServiceLocator.GetInstanceSafe<Configuration.ISettings>();

            var behavior = new BindingRedirectBehavior(settings);

            if (!behavior.IsSkipped)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    try
                    {
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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

#endregion Binding Redirects Stuff

        public Task ExecuteScriptAsync(PackageIdentity identity, string packageInstallPath, string scriptRelativePath, bool throwOnFailure)
        {
            if (ScriptExecutor != null)
            {
                return ScriptExecutor.ExecuteAsync(identity, packageInstallPath, scriptRelativePath, EnvDTEProject, NuGetProjectContext, throwOnFailure);
            }

            return Task.FromResult(false);
        }

        public virtual void BeginProcessing()
        {
            if (ProjectBuildSystem != null)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ProjectBuildSystem.StartBatchEdit();
                });
            }
        }

        public virtual void RegisterProcessedFiles(IEnumerable<string> files)
        {
            // No-op, this is implemented in other project systems, like website.
        }

        public virtual void EndProcessing()
        {
            if (ProjectBuildSystem != null)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ProjectBuildSystem.EndBatchEdit();
                });
            }
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

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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

            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var childItems = await EnvDTEProjectUtility.GetChildItems(EnvDTEProject, path, "*.*", NuGetVSConstants.VsProjectItemKindPhysicalFolder);
                // Get all physical folders
                return from p in childItems
                       select p.Name;
            });
        }

        private static bool IsReferenceUnavailableException(Exception e)
        {
            var comException = e as COMException;

            if (comException == null)
            {
                return false;
            }

            // If VSLangProj.References.Add(...) fails because it could not find the assembly,
            // the HRESULT will be E_FAIL (0x80004005) and the message will be "Reference unavailable."
            return comException.HResult == unchecked((int)0x80004005);
        }
    }
}