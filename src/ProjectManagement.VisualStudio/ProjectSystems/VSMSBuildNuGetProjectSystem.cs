using NuGet.Frameworks;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MicrosoftBuildEvaluationProject = Microsoft.Build.Evaluation.Project;
using MicrosoftBuildEvaluationProjectItem = Microsoft.Build.Evaluation.ProjectItem;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProperty = EnvDTE.Property;
using EnvDTEProjectItem = EnvDTE.ProjectItem;
using EnvDTEProjectItems = EnvDTE.ProjectItems;
using Microsoft.VisualStudio.Shell;

namespace NuGet.ProjectManagement.VisualStudio
{
    public class VSMSBuildNuGetProjectSystem : IMSBuildNuGetProjectSystem
    {
        private const string BinDir = "bin";
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

        public string ProjectFullPath
        {
            get;
            private set;
        }

        public string ProjectName
        {
            get
            {
                return EnvDTEProject.Name;
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

        public void AddFile(string path, Stream stream)
        {
            AddFileCore(path, () => FileSystemUtility.AddFile(ProjectFullPath, path, stream));
        }

        public void AddFile(string path, Action<Stream> writeToStream)
        {
            AddFileCore(path, () => FileSystemUtility.AddFile(ProjectFullPath, path, writeToStream));
        }

        private void AddFileCore(string path, Action addFile)
        {
            bool fileExistsInProject = FileExistsInProject(path);

            // If the file exists on disk but not in the project then skip it.
            // One exception is the 'packages.config' file, in which case we want to include
            // it into the project.
            if (File.Exists(Path.Combine(ProjectFullPath, path)) && !fileExistsInProject && !path.Equals(Constants.PackageReferenceFile))
            {
                NuGetProjectContext.Log(MessageLevel.Warning, Strings.Warning_FileAlreadyExists, path);
            }
            else
            {
                //EnsureCheckedOutIfExists(path);
                addFile();
                if (!fileExistsInProject)
                {
                    AddFileToProject(path);
                }
            }
        }

        private void EnsureCheckedOutIfExists(string path)
        {
            throw new NotImplementedException();
        }

        protected virtual bool ExcludeFile(string path)
        {
            // Exclude files from the bin directory.
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

        public void AddImport(string targetFullPath, ImportLocation location)
        {
            throw new NotImplementedException();
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
                    // This happens if the assembly appears in any of the search paths that VS uses to locate assembly references.
                    // Most commonly, it happens if this assembly is in the GAC or in the output path.
                    if (reference.Path != null && !reference.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
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
                                item.SetMetadataValue("HintPath", referencePath);

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
                        TrySetSpecificVersion(reference);
                        TrySetCopyLocal(reference);
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

        public void RemoveFile(string path)
        {
            throw new NotImplementedException();
        }

        public bool ReferenceExists(string name)
        {
            throw new NotImplementedException();
        }

        public void RemoveImport(string targetFullPath)
        {
            throw new NotImplementedException();
        }

        public void RemoveReference(string name)
        {
            throw new NotImplementedException();
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
        }


        public bool FileExistsInProject(string path)
        {
            return EnvDTEProjectUtility.ContainsFile(EnvDTEProject, path);
        }

        public dynamic GetPropertyValue(string propertyName)
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

        public bool IsSupportedFile(string path)
        {
            string fileName = Path.GetFileName(path);

            // exclude all file names with the pattern as "web.*.config", 
            // e.g. web.config, web.release.config, web.debug.config
            return !(fileName.StartsWith("web.", StringComparison.OrdinalIgnoreCase) &&
                     fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase));
        }

        public string ResolvePath(string path)
        {
            return path;
        }
    }
}
