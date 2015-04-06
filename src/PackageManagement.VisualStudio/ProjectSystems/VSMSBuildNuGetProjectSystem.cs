using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio;
#if VS14
using Microsoft.VisualStudio.ProjectSystem;
#endif
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Frameworks;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProjectItems = EnvDTE.ProjectItems;
using EnvDTEProperty = EnvDTE.Property;
using MicrosoftBuildEvaluationProject = Microsoft.Build.Evaluation.Project;
using MicrosoftBuildEvaluationProjectItem = Microsoft.Build.Evaluation.ProjectItem;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSMSBuildNuGetProjectSystem : IMSBuildNuGetProjectSystem
    {
        private const string BinDir = "bin";
        private const string NuGetImportStamp = "NuGetPackageImportStamp";

        public VSMSBuildNuGetProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
        {
            if(envDTEProject == null)
            {
                throw new ArgumentNullException("envDTEProject");
            }

            if(nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            EnvDTEProject = envDTEProject;
            ProjectFullPath = EnvDTEProjectUtility.GetFullPath(envDTEProject);
            NuGetProjectContext = nuGetProjectContext;
        }

        public EnvDTEProject EnvDTEProject
        {
            get;
            private set;
        }

        public INuGetProjectContext NuGetProjectContext
        {
            get;
            private set;
        }

        private IScriptExecutor _scriptExecutor;
        private IScriptExecutor ScriptExecutor
        {
            get
            {
                if(_scriptExecutor == null)
                {
                    _scriptExecutor = ServiceLocator.GetInstanceSafe<IScriptExecutor>();
                }
                return _scriptExecutor;
            }
        }

        public string ProjectFullPath
        {
            get;
            private set;
        }

        public virtual string ProjectName
        {
            get
            {
                return EnvDTEProject.Name;
            }
        }

        public virtual string ProjectUniqueName
        {
            get
            {
                return EnvDTEProjectUtility.GetCustomUniqueName(EnvDTEProject);
            }
        }

        private NuGetFramework _targetFramework;
        public NuGetFramework TargetFramework
        {
            get
            {
                if (_targetFramework == null)
                {
                    _targetFramework = EnvDTEProjectUtility.GetTargetNuGetFramework(EnvDTEProject) ?? NuGetFramework.UnsupportedFramework;
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
            AddFileCore(path, () => FileSystemUtility.AddFile(ProjectFullPath, path, stream, NuGetProjectContext));
        }

        public virtual void AddFile(string path, Action<Stream> writeToStream)
        {
            AddFileCore(path, () => FileSystemUtility.AddFile(ProjectFullPath, path, writeToStream, NuGetProjectContext));
        }

        private void AddFileCore(string path, Action addFile)
        {
            // Do not try to add file to project, if the path is null or empty.
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            bool fileExistsInProject = FileExistsInProject(path);

            // If the file exists on disk but not in the project then skip it.
            // One exception is the 'packages.config' file, in which case we want to include
            // it into the project.
            // Other exceptions are 'web.config' and 'app.config'
            if (File.Exists(Path.Combine(ProjectFullPath, path))
                && !fileExistsInProject
                && !path.Equals(ProjectManagement.Constants.PackageReferenceFile)
                && !path.Equals(EnvDTEProjectUtility.WebConfig)
                && !path.Equals(EnvDTEProjectUtility.AppConfig))
            {
                NuGetProjectContext.Log(MessageLevel.Warning, Strings.Warning_FileAlreadyExists, path);
            }
            else
            {
                EnsureCheckedOutIfExists(path);
                addFile();
                if (!fileExistsInProject)
                {
                    AddFileToProject(path);
                }
            }
        }

        public void AddExistingFile(string path)
        {
            var fullPath = Path.Combine(ProjectFullPath, path);
            if(!File.Exists(fullPath))
            {
                throw new ArgumentNullException(String.Format(Strings.PathToExistingFileNotPresent, fullPath, ProjectName));
            }

            AddFileCore(path, () => { });
        }

        private void EnsureCheckedOutIfExists(string path)
        {
            EnvDTEProjectUtility.EnsureCheckedOutIfExists(EnvDTEProject, ProjectFullPath, path);
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

        protected virtual void AddFileToProject(string path)
        {
            if (ExcludeFile(path))
            {
                return;
            }

            // Get the project items for the folder path
            string folderPath = Path.GetDirectoryName(path);
            string fullPath = FileSystemUtility.GetFullPath(ProjectFullPath, path);

            ThreadHelper.Generic.Invoke(() =>
            {
                EnvDTEProjectItems container = EnvDTEProjectUtility.GetProjectItems(EnvDTEProject, folderPath, createIfNotExists: true);
                // Add the file to project or folder
                AddFileToContainer(fullPath, folderPath, container);
            });

            NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_AddedFileToProject, path, ProjectName);
        }

        protected virtual void AddFileToContainer(string fullPath, string folderPath, EnvDTEProjectItems container)
        {
            container.AddFromFileCopy(fullPath);
        }

        public void AddFrameworkReference(string name)
        {
            try
            {
                // Add a reference to the project
                AddGacReference(name);

                NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_AddGacReference, name, ProjectName);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Strings.FailedToAddGacReference, name), e);
            }
        }

        protected virtual void AddGacReference(string name)
        {
            EnvDTEProjectUtility.GetReferences(EnvDTEProject).Add(name);
        }

        public virtual void AddImport(string targetFullPath, ImportLocation location)
        {
            if (String.IsNullOrEmpty(targetFullPath))
            {
                throw new ArgumentNullException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "targetPath");
            }

            string relativeTargetPath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(EnvDTEProjectUtility.GetFullPath(EnvDTEProject)), targetFullPath);
            EnvDTEProjectUtility.AddImportStatement(EnvDTEProject, relativeTargetPath, location);

            EnvDTEProjectUtility.Save(EnvDTEProject);

            // notify the project system of the change
            UpdateImportStamp(EnvDTEProject);
        }

        public virtual void AddReference(string referencePath)
        {
            if(referencePath == null)
            {
                throw new ArgumentNullException("referencePath");
            }

            string name = Path.GetFileNameWithoutExtension(referencePath);

            try
            {
                // Get the full path to the reference
                string fullPath = Path.Combine(ProjectFullPath, referencePath);

                string assemblyPath = fullPath;
                bool usedTempFile = false;

                // There is a bug in Visual Studio whereby if the fullPath contains a comma, 
                // then calling Project.Object.References.Add() on it will throw a COM exception.
                // To work around it, we copy the assembly into temp folder and add reference to the copied assembly
                if (fullPath.Contains(","))
                {
                    string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(fullPath));
                    File.Copy(fullPath, tempFile, true);
                    assemblyPath = tempFile;
                    usedTempFile = true;
                }

                // Add a reference to the project
                dynamic reference = EnvDTEProjectUtility.GetReferences(EnvDTEProject).Add(assemblyPath);

                // if we copied the assembly to temp folder earlier, delete it now since we no longer need it.
                if (usedTempFile)
                {
                    try
                    {
                        File.Delete(assemblyPath);
                    }
                    catch
                    {
                        // don't care if we fail to delete a temp file
                    }
                }

                if (reference != null)
                {
                    var path = GetReferencePath(reference);

                    // If path != fullPath, we need to set CopyLocal thru msbuild by setting Private 
                    // to true.
                    // This happens if the assembly appears in any of the search paths that VS uses to 
                    // locate assembly references.
                    // Most commonly, it happens if this assembly is in the GAC or in the output path.
                    if (path != null && !path.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
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
                                FileSystemUtility.MakeWriteable(EnvDTEProject.FullName);
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

                NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_AddReference, name, ProjectName);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Strings.FailedToAddReference, name), e);
            }
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
            if (obj1 != null && obj2 != null)
            {
                return equals(obj1, obj2);
            }

            // Otherwise consider them equal if either of the values are null
            return true;
        }

        public virtual void RemoveFile(string path)
        {
            if (EnvDTEProjectUtility.DeleteProjectItem(EnvDTEProject, path))
            {
                string folderPath = Path.GetDirectoryName(path);
                if (!String.IsNullOrEmpty(folderPath))
                {
                    NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFileFromFolder, Path.GetFileName(path), folderPath);
                }
                else
                {
                    NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFile, Path.GetFileName(path));
                }
            }
        }

        public virtual bool ReferenceExists(string name)
        {
            try
            {
                string referenceName = name;
                if (ProjectManagement.Constants.AssemblyReferencesExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
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
        }

        public virtual void RemoveImport(string targetFullPath)
        {
            if (String.IsNullOrEmpty(targetFullPath))
            {
                throw new ArgumentNullException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "targetPath");
            }

            string relativeTargetPath = PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(EnvDTEProjectUtility.GetFullPath(EnvDTEProject)), targetFullPath);
            EnvDTEProjectUtility.RemoveImportStatement(EnvDTEProject, relativeTargetPath);

            EnvDTEProjectUtility.Save(EnvDTEProject);

            // notify the project system of the change
            UpdateImportStamp(EnvDTEProject);
        }

        public virtual void RemoveReference(string name)
        {
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
                    NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemoveReference, name, ProjectName);
                }
            }
            catch (Exception e)
            {
                NuGetProjectContext.Log(MessageLevel.Warning, e.Message);
            }
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
            catch (System.Runtime.InteropServices.COMException)
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
            catch (System.Runtime.InteropServices.COMException)
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
            catch (System.Runtime.InteropServices.COMException)
            {
            }
        }


        public virtual bool FileExistsInProject(string path)
        {
            return EnvDTEProjectUtility.ContainsFile(EnvDTEProject, path);
        }

        public virtual dynamic GetPropertyValue(string propertyName)
        {
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
        }

        public virtual bool IsSupportedFile(string path)
        {
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
        /// Sets NuGetPackageImportStamp to a new random guid. This is a hack to let the project system know it is out of date.
        /// The value does not matter, it just needs to change.
        /// </summary>
        protected static void UpdateImportStamp(EnvDTEProject envDTEProject, bool isCpsProjectSystem = false)
        {
            // There is no reason to call this for pre-Dev12 project systems.
            if (VSVersionHelper.VsMajorVersion >= 12)
            {
#if VS14
                // Switch to UI thread to update Import Stamp for Dev14.
                if (isCpsProjectSystem && VSVersionHelper.IsVisualStudio2014)
                {
                    try
                    {
                        var projectServiceAccessor = ServiceLocator.GetInstance<IProjectServiceAccessor>();
                        ProjectService projectService = projectServiceAccessor.GetProjectService();
                        IThreadHandling threadHandling = projectService.Services.ThreadingPolicy;
                        threadHandling.SwitchToUIThread();
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.WriteToActivityLog(ex);
                    }
                }
#endif

                IVsBuildPropertyStorage propStore = VsHierarchyUtility.ToVsHierarchy(envDTEProject) as IVsBuildPropertyStorage;
                if (propStore != null)
                {
                    // <NuGetPackageImportStamp>af617720</NuGetPackageImportStamp>
                    string stamp = Guid.NewGuid().ToString().Split('-')[0];
                    try
                    {
                        int r1 = propStore.SetPropertyValue(NuGetImportStamp, string.Empty, (uint)_PersistStorageType.PST_PROJECT_FILE, stamp);
                    }
                    catch (Exception ex1)
                    {
                        ExceptionHelper.WriteToActivityLog(ex1);
                    }

                    // Remove the NuGetImportStamp so that VC++ project file won't be updated with this stamp on disk,
                    // which causes unnecessary source control pending changes. 
                    try
                    {
                        int r2 = propStore.RemoveProperty(NuGetImportStamp, string.Empty, (uint)_PersistStorageType.PST_PROJECT_FILE);
                    }
                    catch (Exception ex2)
                    {
                        ExceptionHelper.WriteToActivityLog(ex2);
                    }
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
            InitForBindingRedirects();
            if(IsBindingRedirectSupported && VSSolutionManager != null)
            {
                RuntimeHelpers.AddBindingRedirects(VSSolutionManager, EnvDTEProject, VSFrameworkMultiTargeting, NuGetProjectContext);
            }
        }

        private bool BindingRedirectsRelatedInitialized = false;
        private VSSolutionManager VSSolutionManager { get; set; }
        private IVsFrameworkMultiTargeting VSFrameworkMultiTargeting { get; set; }

        private void InitForBindingRedirects()
        {
            if(!BindingRedirectsRelatedInitialized)
            {
                var solutionManager = ServiceLocator.GetInstanceSafe<ISolutionManager>();
                VSSolutionManager = (solutionManager != null) ? (solutionManager as VSSolutionManager) : null;
                VSFrameworkMultiTargeting = ServiceLocator.GetGlobalService<SVsFrameworkMultiTargeting, IVsFrameworkMultiTargeting>();
            }
        }
        #endregion

        public async System.Threading.Tasks.Task ExecuteScriptAsync(string packageInstallPath, string scriptRelativePath, ZipArchive packageZipArchive, NuGetProject nuGetProject)
        {
            if (ScriptExecutor != null)
            {
                await ScriptExecutor.ExecuteAsync(packageInstallPath, scriptRelativePath, packageZipArchive, EnvDTEProject, nuGetProject, NuGetProjectContext);
            }
        }


        public virtual void BeginProcessing(IEnumerable<string> files)
        {
        }

        public virtual void EndProcessing()
        {
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            // Only delete this folder if it is empty and we didn't specify that we want to recurse
            if (!recursive && (FileSystemUtility.GetFiles(ProjectFullPath, path, "*.*", recursive).Any() || FileSystemUtility.GetDirectories(ProjectFullPath, path).Any()))
            {
                NuGetProjectContext.Log(MessageLevel.Warning, NuGet.ProjectManagement.Strings.Warning_DirectoryNotEmpty, path);
                return;
            }

            // Workaround for TFS update issue. If we're bound to TFS, do not try and delete directories.
            if (SourceControlUtility.GetSourceControlManager(NuGetProjectContext) == null && EnvDTEProjectUtility.DeleteProjectItem(EnvDTEProject, path))
            {
                NuGetProjectContext.Log(MessageLevel.Debug, NuGet.ProjectManagement.Strings.Debug_RemovedFolder, path);
            }
        }

        public IEnumerable<string> GetFiles(string path, string filter, bool recursive)
        {
            if (recursive)
            {
                throw new NotSupportedException();
            }
            else
            {
                // Get all physical files
                return from p in EnvDTEProjectUtility.GetChildItems(EnvDTEProject, path, filter, NuGetVSConstants.VsProjectItemKindPhysicalFile)
                       select p.Name;
            }
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            // Get all physical folders
            return from p in EnvDTEProjectUtility.GetChildItems(EnvDTEProject, path, "*.*", NuGetVSConstants.VsProjectItemKindPhysicalFolder)
                   select p.Name;
        }
    }
}
