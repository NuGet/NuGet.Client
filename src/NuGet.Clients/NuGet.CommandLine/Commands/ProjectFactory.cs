﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Xml.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CommandLine
{

    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    public class ProjectFactory : MSBuildUser, IProjectFactory, IPropertyProvider
    {
        // Its type is Microsoft.Build.Evaluation.Project
        private dynamic _project;

        private Common.ILogger _logger;
        private bool _usingJsonFile;

        // Files we want to always exclude from the resulting package
        private static readonly HashSet<string> _excludeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            NuGetConstants.PackageReferenceFile,
            "Web.Debug.config",
            "Web.Release.config"
        };

        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Packaging folders
        private const string ContentFolder = "content";

        private const string ReferenceFolder = "lib";
        private const string ToolsFolder = "tools";
        private const string SourcesFolder = "src";

        // Common item types
        private const string SourcesItemType = "Compile";

        private const string ContentItemType = "Content";
        private const string ProjectReferenceItemType = "ProjectReference";
        private const string ReferenceOutputAssembly = "ReferenceOutputAssembly";
        private const string PackagesFolder = "packages";
        private const string TransformFileExtension = ".transform";

        [Import]
        public Configuration.IMachineWideSettings MachineWideSettings { get; set; }

        public static IProjectFactory ProjectCreator(PackArgs packArgs, string path)
        {
            return new ProjectFactory(packArgs.MsBuildDirectory.Value, path, packArgs.Properties)
            {
                IsTool = packArgs.Tool,
                LogLevel = packArgs.LogLevel,
                Logger = packArgs.Logger,
                MachineWideSettings = packArgs.MachineWideSettings,
                Build = packArgs.Build,
                IncludeReferencedProjects = packArgs.IncludeReferencedProjects
            };
        }

        public ProjectFactory(string msbuildDirectory, string path, IDictionary<string, string> projectProperties)
        {
            LoadAssemblies(msbuildDirectory);

            // create project            
            var project = Activator.CreateInstance(
                _projectType,
                path,
                projectProperties,
                null);
            Initialize(project);
        }

        public ProjectFactory(string msbuildDirectory, dynamic project)
        {
            LoadAssemblies(msbuildDirectory);
            Initialize(project);
        }

        private ProjectFactory(
            string msbuildDirectory,
            Assembly msbuildAssembly,
            Assembly frameworkAssembly,
            dynamic project)
        {
            _msbuildDirectory = msbuildDirectory;
            _msbuildAssembly = msbuildAssembly;
            _frameworkAssembly = frameworkAssembly;
            LoadTypes();
            Initialize(project);
        }

        private void Initialize(dynamic project)
        {
            _project = project;
            ProjectProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddSolutionDir();

            // Get the target framework of the project
            string targetFrameworkMoniker = _project.GetPropertyValue("TargetFrameworkMoniker");
            if (!String.IsNullOrEmpty(targetFrameworkMoniker))
            {
                TargetFramework = new FrameworkName(targetFrameworkMoniker);
            }

            IConsole console = Logger as IConsole;
            switch (LogLevel)
            {
                case LogLevel.Verbose:
                {
                    console.Verbosity = Verbosity.Detailed;
                    break;
                }
                case LogLevel.Information:
                {
                    console.Verbosity = Verbosity.Normal;
                    break;
                }
                case LogLevel.Minimal:
                {
                    console.Verbosity = Verbosity.Quiet;
                    break;
                }
            }                
        }

        private string TargetPath
        {
            get;
            set;
        }

        private FrameworkName TargetFramework
        {
            get;
            set;
        }

        public void SetIncludeSymbols(bool includeSymbols)
        {
            IncludeSymbols = includeSymbols;
        }
        public bool IncludeSymbols { get; set; }

        public bool IncludeReferencedProjects { get; set; }

        public bool Build { get; set; }

        public Dictionary<string, string> GetProjectProperties()
        {
            return ProjectProperties;
        }
        public Dictionary<string, string> ProjectProperties { get; private set; }

        public bool IsTool { get; set; }

        public LogLevel LogLevel { get; set; }

        public Common.ILogger Logger
        {
            get
            {
                return _logger ?? Common.NullLogger.Instance;
            }
            set
            {
                _logger = value;
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to continue regardless of any error we encounter extracting metadata.")]
        public Packaging.PackageBuilder CreateBuilder(string basePath, NuGetVersion version, string suffix, bool buildIfNeeded)
        {
            if (buildIfNeeded)
            {
                BuildProject();
            }

            if (!string.IsNullOrEmpty(TargetPath))
            {
                Logger.LogMinimal(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("PackagingFilesFromOutputPath"),
                        Path.GetFullPath(Path.GetDirectoryName(TargetPath))));
            }

            var builder = new Packaging.PackageBuilder();

            try
            {
                // Populate the package builder with initial metadata from the assembly/exe
                if (!Directory.Exists(TargetPath))
                {
                    AssemblyMetadataExtractor.ExtractMetadata(builder, TargetPath);
                }
                else
                {
                    ExtractMetadataFromProject(builder);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("UnableToExtractAssemblyMetadata"),
                        Path.GetFileName(TargetPath)));

                IConsole console = Logger as IConsole;
                if (console != null && console.Verbosity == Verbosity.Detailed)
                {
                    Logger.LogError(ex.ToString());
                }
                else
                {
                    Logger.LogError(ex.Message);
                }

                return null;
            }

            var projectAuthor = InitializeProperties(builder);

            // Only override properties from assembly extracted metadata if they haven't 
            // been specified also at construction time for the factory (that is, 
            // console properties always take precedence.
            foreach (var key in builder.Properties.Keys)
            {
                if (!_properties.ContainsKey(key) &&
                    !ProjectProperties.ContainsKey(key))
                {
                    _properties.Add(key, builder.Properties[key]);
                }
            }

            Packaging.Manifest manifest = null;

            // If there is a project.json file, load that and skip any nuspec that may exist
            if (!PackCommandRunner.ProcessProjectJsonFile(builder, basePath, builder.Id, version, suffix, GetPropertyValue))
            {
                // If the package contains a nuspec file then use it for metadata
                manifest = ProcessNuspec(builder, basePath);
            }
            else
            {
                _usingJsonFile = true;
            }

            // Remove the extra author
            if (builder.Authors.Count > 1)
            {
                builder.Authors.Remove(projectAuthor);
            }

            // Add output files
            ApplyAction(p => p.AddOutputFiles(builder));

            // Add content files if there are any. They could come from a project or nuspec file
            ApplyAction(p => p.AddFiles(builder, ContentItemType, ContentFolder));

            // Add sources if this is a symbol package
            if (IncludeSymbols)
            {
                ApplyAction(p => p.AddFiles(builder, SourcesItemType, SourcesFolder));
            }

            ProcessDependencies(builder);

            // Set defaults if some required fields are missing
            if (String.IsNullOrEmpty(builder.Description))
            {
                builder.Description = "Description";
                Logger.LogWarning(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("Warning_UnspecifiedField"),
                        "Description",
                        "Description"));
            }

            if (!builder.Authors.Any())
            {
                builder.Authors.Add(Environment.UserName);
                Logger.LogWarning(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("Warning_UnspecifiedField"),
                        "Author",
                        Environment.UserName));
            }

            return builder;
        }

        public string InitializeProperties(Packaging.IPackageMetadata metadata)
        {
            // Set the properties that were resolved from the assembly/project so they can be
            // resolved by name if the nuspec contains tokens
            _properties.Clear();

            // Allow Id to be overriden by cmd line properties
            if (ProjectProperties.ContainsKey("Id"))
            {
                _properties.Add("Id", ProjectProperties["Id"]);
            }
            else
            {
                _properties.Add("Id", metadata.Id);
            }

            _properties.Add("Version", metadata.Version.ToString());

            if (!String.IsNullOrEmpty(metadata.Title))
            {
                _properties.Add("Title", metadata.Title);
            }

            if (!String.IsNullOrEmpty(metadata.Description))
            {
                _properties.Add("Description", metadata.Description);
            }

            if (!String.IsNullOrEmpty(metadata.Copyright))
            {
                _properties.Add("Copyright", metadata.Copyright);
            }

            string projectAuthor = metadata.Authors.FirstOrDefault();
            if (!String.IsNullOrEmpty(projectAuthor))
            {
                _properties.Add("Author", projectAuthor);
            }
            return projectAuthor;
        }

        public string GetPropertyValue(string propertyName)
        {
            string value;
            if (!_properties.TryGetValue(propertyName, out value) &&
                !ProjectProperties.TryGetValue(propertyName, out value))
            {
                dynamic property = _project.GetProperty(propertyName);
                if (property != null)
                {
                    value = property.EvaluatedValue;
                }
            }

            return value;
        }

        dynamic IPropertyProvider.GetPropertyValue(string propertyName)
        {
            return GetPropertyValue(propertyName);
        }

        private void BuildProject()
        {
            if (Build)
            {
                if (TargetFramework != null)
                {
                    Logger.LogMinimal(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("BuildingProjectTargetingFramework"),
                            _project.FullPath,
                            TargetFramework));
                }

                BuildProjectWithMsbuild();
            }
            else
            {
                TargetPath = ResolveTargetPath();

                // Make if the target path doesn't exist, fail
                if (!Directory.Exists(TargetPath) && !File.Exists(TargetPath))
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("UnableToFindBuildOutput"), TargetPath);
                }
            }
        }

        private void BuildProjectWithMsbuild()
        {
            string properties = string.Empty;
            foreach (var property in ProjectProperties)
            {
                if (property.Value.Contains(" "))
                {
                    properties += $" /p:{property.Key}=\"{property.Value}\"";
                }
                else
                {
                    properties += $" /p:{property.Key}={property.Value}";
                }
            }

            int result = MsBuildUtility.Build(_msbuildDirectory, $"\"{_project.FullPath}\" {properties} /toolsversion:{_project.ToolsVersion}");

            if ((int)Microsoft.Build.Execution.BuildResultCode.Failure == result)
            {
                // If the build fails, report the error
                throw new CommandLineException(LocalizedResourceManager.GetString("FailedToBuildProject"), Path.GetFileName(_project.FullPath));
            }

            TargetPath = ResolveTargetPath();
        }

        private string ResolveTargetPath()
        {
            // Set the project properties
            foreach (var property in ProjectProperties)
            {
                var existingProperty = _project.GetProperty(property.Key);
                if (existingProperty == null || !IsGlobalProperty(existingProperty))
                {
                    // Only set the property if it's not already defined as a global property
                    // (which those passed in via the ctor are) as trying to set global properties
                    // with this method throws.
                    _project.SetProperty(property.Key, property.Value);
                }
            }

            // Re-evaluate the project so that the new property values are applied
            _project.ReevaluateIfNecessary();

            // Return the new target path
            string targetPath = _project.GetPropertyValue("TargetPath");

            if (string.IsNullOrEmpty(targetPath))
            {
                string outputPath = _project.GetPropertyValue("OutputPath");
                string configuration = _project.GetPropertyValue("Configuration");
                string projectName = Path.GetFileName(Path.GetDirectoryName(_project.FullPath));
                targetPath = PathUtility.EnsureTrailingSlash(Path.Combine(outputPath, projectName, "bin", configuration));
            }

            return targetPath;
        }

        // The type of projectProperty is Microsoft.Build.Evaluation.ProjectProperty
        private static bool IsGlobalProperty(object projectProperty)
        {
            // This property isn't available on xbuild (mono)            
            var property = projectProperty.GetType().GetProperty("IsGlobalProperty", BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                return (bool)property.GetValue(projectProperty, null);
            }

            // REVIEW: Maybe there's something better we can do on mono
            // Just return false if the property isn't there
            return false;
        }

        private void ExtractMetadataFromProject(Packaging.PackageBuilder builder)
        {
            builder.Id = builder.Id ??
                        _project.GetPropertyValue("AssemblyName") ??
                        Path.GetFileNameWithoutExtension(_project.FullPath);

            string version = _project.GetPropertyValue("Version");
            if (builder.Version == null)
            {
                NuGetVersion parsedVersion;

                if (NuGetVersion.TryParse(version, out parsedVersion))
                {
                    builder.Version = parsedVersion;
                }
                else
                {
                    builder.Version = new NuGetVersion(1, 0, 0);
                }
            }
        }

        private static IEnumerable<string> GetFiles(string path, string fileNameWithoutExtension, HashSet<string> allowedExtensions, SearchOption searchOption)
        {
            return allowedExtensions.Select(extension => Directory.GetFiles(path, fileNameWithoutExtension + extension, searchOption)).SelectMany(a => a);
        }

        private void ApplyAction(Action<ProjectFactory> action)
        {
            if (IncludeReferencedProjects)
            {
                RecursivelyApply(action);
            }
            else
            {
                action(this);
            }
        }

        /// <summary>
        /// Recursively execute the specified action on the current project and
        /// projects referenced by the current project.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        private void RecursivelyApply(Action<ProjectFactory> action)
        {
            var projectCollection = Activator.CreateInstance(_projectCollectionType) as IDisposable;
            using (projectCollection)
            {
                RecursivelyApply(action, projectCollection);
            }
        }

        /// <summary>
        /// Recursively execute the specified action on the current project and
        /// projects referenced by the current project.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="alreadyAppliedProjects">The collection of projects that have been processed.
        /// It is used to avoid processing the same project more than once.</param>
        private void RecursivelyApply(Action<ProjectFactory> action, dynamic alreadyAppliedProjects)
        {
            action(this);
            foreach (var item in _project.GetItems(ProjectReferenceItemType))
            {
                if (ShouldExcludeItem(item))
                {
                    continue;
                }

                string fullPath = item.GetMetadataValue("FullPath");
                if (!string.IsNullOrEmpty(fullPath) &&
                    !NuspecFileExists(fullPath) &&
                    !File.Exists(ProjectJsonPathUtilities.GetProjectConfigPath(Path.GetDirectoryName(fullPath), Path.GetFileName(fullPath))) &&
                    alreadyAppliedProjects.GetLoadedProjects(fullPath).Count == 0)
                {
                    dynamic project = Activator.CreateInstance(
                        _projectType,
                        fullPath,
                        null,
                        null,
                        alreadyAppliedProjects);
                    var referencedProject = new ProjectFactory(
                        _msbuildDirectory, _msbuildAssembly, _frameworkAssembly, project);
                    referencedProject.Logger = _logger;
                    referencedProject.IncludeSymbols = IncludeSymbols;
                    referencedProject.Build = Build;
                    referencedProject.IncludeReferencedProjects = IncludeReferencedProjects;
                    referencedProject.ProjectProperties = ProjectProperties;
                    referencedProject.TargetFramework = TargetFramework;
                    referencedProject.BuildProject();
                    referencedProject.RecursivelyApply(action, alreadyAppliedProjects);
                }
            }
        }

        /// <summary>
        /// Should the project item be excluded based on the Reference output assembly metadata
        /// </summary>
        /// <param name="item">Dynamic item which is a project item</param>
        /// <returns>true, if the item should be excluded. false, otherwise.</returns>
        private static bool ShouldExcludeItem(dynamic item)
        {
            if (item == null)
            {
                return true;
            }

            if (item.HasMetadata(ReferenceOutputAssembly))
            {
                bool result;
                if (bool.TryParse(item.GetMetadataValue("ReferenceOutputAssembly"), out result))
                {
                    if (!result)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether a project file has a corresponding nuspec file.
        /// </summary>
        /// <param name="projectFileFullName">The name of the project file.</param>
        /// <returns>True if there is a corresponding nuspec file.</returns>
        private static bool NuspecFileExists(string projectFileFullName)
        {
            var nuspecFile = Path.ChangeExtension(projectFileFullName, NuGetConstants.ManifestExtension);
            return File.Exists(nuspecFile);
        }

        /// <summary>
        /// Adds referenced projects that have corresponding nuspec files as dependencies.
        /// </summary>
        /// <param name="dependencies">The dependencies collection where the new dependencies
        /// are added into.</param>
        private void AddProjectReferenceDependencies(Dictionary<string, Packaging.Core.PackageDependency> dependencies)
        {
            var processedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var projectsToProcess = new Queue<object>();
            dynamic projectCollection = Activator.CreateInstance(_projectCollectionType);
            using ((IDisposable)projectCollection)
            {
                projectsToProcess.Enqueue(_project);
                while (projectsToProcess.Count > 0)
                {
                    dynamic project = projectsToProcess.Dequeue();
                    processedProjects.Add(project.FullPath);

                    foreach (var projectReference in project.GetItems(ProjectReferenceItemType))
                    {
                        if (ShouldExcludeItem(projectReference))
                        {
                            continue;
                        }

                        string fullPath = projectReference.GetMetadataValue("FullPath");
                        if (string.IsNullOrEmpty(fullPath) ||
                            processedProjects.Contains(fullPath))
                        {
                            continue;
                        }

                        var loadedProjects = projectCollection.GetLoadedProjects(fullPath);
                        var referencedProject = loadedProjects.Count > 0 ?
                            loadedProjects[0] :
                            Activator.CreateInstance(
                                _projectType,
                                fullPath,
                                project.GlobalProperties,
                                null,
                                projectCollection);

                        if (NuspecFileExists(fullPath) || File.Exists(ProjectJsonPathUtilities.GetProjectConfigPath(Path.GetDirectoryName(fullPath), Path.GetFileName(fullPath))))
                        {
                            var dependency = CreateDependencyFromProject(referencedProject, dependencies);
                            dependencies[dependency.Id] = dependency;
                        }
                        else
                        {
                            projectsToProcess.Enqueue(referencedProject);
                        }
                    }
                }
            }
        }

        private bool ProcessJsonFile(Packaging.PackageBuilder builder, string basePath, string id)
        {
            return PackCommandRunner.ProcessProjectJsonFile(builder, basePath, id, null, null, GetPropertyValue);
        }

        // Creates a package dependency from the given project, which has a corresponding
        // nuspec file.
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to continue regardless of any error we encounter extracting metadata.")]
        private Packaging.Core.PackageDependency CreateDependencyFromProject(dynamic project, Dictionary<string, Packaging.Core.PackageDependency> dependencies)
        {
            try
            {
                var projectFactory = new ProjectFactory(_msbuildDirectory, _msbuildAssembly, _frameworkAssembly, project);
                projectFactory.Build = Build;
                projectFactory.ProjectProperties = ProjectProperties;
                projectFactory.BuildProject();
                var builder = new Packaging.PackageBuilder();

                // If building an xproj, then TargetPath points to the folder where the framework folders will be
                // instead of to a single dll. Skip trying to ExtractMetadata from the dll and instead
                // use only metadata from the project and json file.
                if (!Directory.Exists(projectFactory.TargetPath))
                {
                    try
                    {
                        AssemblyMetadataExtractor.ExtractMetadata(builder, projectFactory.TargetPath);
                    }
                    catch
                    {
                        projectFactory.ExtractMetadataFromProject(builder);
                    }
                }
                else
                {
                    projectFactory.ExtractMetadataFromProject(builder);
                }

                projectFactory.InitializeProperties(builder);

                if (!projectFactory.ProcessJsonFile(builder, project.DirectoryPath, null))
                {
                    projectFactory.ProcessNuspec(builder, null);
                }

                VersionRange versionRange = null;
                if (dependencies.ContainsKey(builder.Id))
                {
                    VersionRange nuspecVersion = dependencies[builder.Id].VersionRange;
                    if (nuspecVersion != null)
                    {
                        versionRange = nuspecVersion;
                    }
                }

                if (versionRange == null)
                {
                    versionRange = VersionRange.Parse(builder.Version.ToString());
                }

                return new Packaging.Core.PackageDependency(
                    builder.Id,
                    versionRange);
            }
            catch (Exception ex)
            {
                var message = string.Format(
                    CultureInfo.InvariantCulture,
                    LocalizedResourceManager.GetString("Error_ProcessingNuspecFile"),
                    project.FullPath,
                    ex.Message);
                throw new CommandLineException(message, ex);
            }
        }

        private void AddOutputFiles(Packaging.PackageBuilder builder)
        {
            // Get the target framework of the project
            FrameworkName targetFramework = TargetFramework;

            // Get the target file path
            string targetPath = TargetPath;

            // List of extensions to allow in the output path
            var allowedOutputExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".dll",
                ".exe",
                ".xml",
                ".winmd"
            };

            if (IncludeSymbols)
            {
                // Include pdbs for symbol packages
                allowedOutputExtensions.Add(".pdb");
            }

            string projectOutputDirectory = Path.GetDirectoryName(targetPath);

            string targetFileName;
            if (Directory.Exists(targetPath))
            {
                targetFileName = builder.Id;
            }
            else
            {
                targetFileName = Path.GetFileNameWithoutExtension(TargetPath);
            }

            // By default we add all files in the project's output directory
            foreach (var file in GetFiles(projectOutputDirectory, targetFileName, allowedOutputExtensions, SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);

                // Only look at files we care about
                if (!allowedOutputExtensions.Contains(extension))
                {
                    continue;
                }

                string targetFolder;

                if (IsTool)
                {
                    targetFolder = ToolsFolder;
                }
                else
                {
                    if (Directory.Exists(TargetPath))
                    {
                        targetFolder = Path.Combine(ReferenceFolder, Path.GetDirectoryName(file.Replace(TargetPath, string.Empty)));
                    }
                    else if (targetFramework == null)
                    {
                        targetFolder = ReferenceFolder;
                    }
                    else
                    {
                        NuGetFramework nugetFramework = NuGetFramework.Parse(targetFramework.FullName);
                        string shortFolderName = nugetFramework.GetShortFolderName();
                        targetFolder = Path.Combine(ReferenceFolder, shortFolderName);
                    }
                }
                var packageFile = new Packaging.PhysicalPackageFile
                {
                    SourcePath = file,
                    TargetPath = Path.Combine(targetFolder, Path.GetFileName(file))
                };
                AddFileToBuilder(builder, packageFile);
            }
        }

        private void ProcessDependencies(Packaging.PackageBuilder builder)
        {
            // get all packages and dependencies, including the ones in project references
            var packagesAndDependencies = new Dictionary<String, Tuple<PackageReaderBase, Packaging.Core.PackageDependency>>();
            ApplyAction(p => p.AddDependencies(packagesAndDependencies));

            // list of all dependency packages
            var packages = packagesAndDependencies.Values.Select(t => t.Item1).ToList();

            // Add the transform file to the package builder
            ProcessTransformFiles(builder, packages.SelectMany(GetTransformFiles));

            var dependencies = new Dictionary<string, Packaging.Core.PackageDependency>();
            if (!_usingJsonFile)
            {
                dependencies = builder.DependencyGroups.SelectMany(d => d.Packages)
                .ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
            }

            // Reduce the set of packages we want to include as dependencies to the minimal set.
            // Normally, packages.config has the full closure included, we only add top level
            // packages, i.e. packages with in-degree 0
            foreach (var package in packages)
            {
                // Don't add duplicate dependencies
                if (dependencies.ContainsKey(package.GetIdentity().Id) || !FindDependency(package.GetIdentity(), packagesAndDependencies.Values))
                {
                    continue;
                }

                var dependency = packagesAndDependencies[package.GetIdentity().Id].Item2;
                dependencies[dependency.Id] = dependency;
            }

            DisposePackageReaders(packagesAndDependencies);

            if (IncludeReferencedProjects)
            {
                AddProjectReferenceDependencies(dependencies);
            }

            if (_usingJsonFile)
            {
                if (dependencies.Any())
                {
                    if (builder.DependencyGroups.Any())
                    {
                        var i = 0;
                        foreach (var group in builder.DependencyGroups.ToList())
                        {
                            List<Packaging.Core.PackageDependency> newPackagesList = new List<Packaging.Core.PackageDependency>(group.Packages);
                            foreach (var dependency in dependencies)
                            {
                                if (!newPackagesList.Contains(dependency.Value))
                                {
                                    newPackagesList.Add(dependency.Value);
                                }
                            }

                            var dependencyGroup = new PackageDependencyGroup(group.TargetFramework, newPackagesList);

                            builder.DependencyGroups.RemoveAt(i);
                            builder.DependencyGroups.Insert(i, dependencyGroup);

                            i++;
                        }
                    }
                    else
                    {
                        builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies.Values));
                    }
                }
            }
            else
            {
                // TO FIX: when we persist the target framework into packages.config file,
                // we need to pull that info into building the PackageDependencySet object
                builder.DependencyGroups.Clear();

                // REVIEW: IS NuGetFramework.AnyFramework correct?
                builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies.Values));
            }
        }

        private bool FindDependency(PackageIdentity projectPackage, IEnumerable<Tuple<PackageReaderBase, Packaging.Core.PackageDependency>> packagesAndDependencies)
        {
            // returns true if the dependency should be added to the package
            // This happens if the dependency is not a dependency of a dependecy
            // Or if the project dependency version is != the dependency's dependency version
            bool found = false;
            foreach (var reader in packagesAndDependencies)
            {
                foreach (var set in reader.Item1.GetPackageDependencies())
                {
                    foreach (var dependency in set.Packages)
                    {
                        if (dependency.Id.Equals(projectPackage.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;

                            if (dependency.VersionRange.MinVersion < projectPackage.Version ||
                                (!dependency.VersionRange.IsMinInclusive &&
                                dependency.VersionRange.MinVersion == projectPackage.Version))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return !found;
        }

        private void AddDependencies(Dictionary<String, Tuple<PackageReaderBase, Packaging.Core.PackageDependency>> packagesAndDependencies)
        {
            Dictionary<string, object> props = new Dictionary<string, object>();

            foreach (var property in _project.Properties)
            {
                props.Add(property.Name, property.EvaluatedValue);
            }

            if (!props.ContainsKey(ProjectManagement.NuGetProjectMetadataKeys.TargetFramework))
            {
                props.Add(ProjectManagement.NuGetProjectMetadataKeys.TargetFramework, new NuGetFramework(TargetFramework.Identifier, TargetFramework.Version, TargetFramework.Profile));
            }
            if (!props.ContainsKey(ProjectManagement.NuGetProjectMetadataKeys.Name))
            {
                props.Add(ProjectManagement.NuGetProjectMetadataKeys.Name, Path.GetFileNameWithoutExtension(_project.FullPath));
            }

            ProjectManagement.PackagesConfigNuGetProject packagesProject = new ProjectManagement.PackagesConfigNuGetProject(_project.DirectoryPath, props);

            if (!packagesProject.PackagesConfigExists())
            {
                return;
            }
            Logger.LogMinimal(LocalizedResourceManager.GetString("UsingPackagesConfigForDependencies"));

            var references = packagesProject.GetInstalledPackagesAsync(CancellationToken.None).Result;

            var solutionDir = GetSolutionDir();
            string packagesFolderPath;
            if (solutionDir == null)
            {
                packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(_project.DirectoryPath, ReadSettings(_project.DirectoryPath));
            }
            else
            {
                packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(solutionDir, ReadSettings(solutionDir));
            }
            var sourceRepository = Repository.Factory.GetCoreV3(packagesFolderPath).GetResource<FindPackageByIdResource>();

            // Collect all packages
            IDictionary<PackageIdentity, Packaging.PackageReference> packageReferences =
                references
                .Where(r => !r.IsDevelopmentDependency)
                .ToDictionary(r => r.PackageIdentity);
            // add all packages and create an associated dependency to the dictionary
            foreach (Packaging.PackageReference reference in packageReferences.Values)
            {
                var packageReference = references.FirstOrDefault(r => r.PackageIdentity == reference.PackageIdentity);
                if (packageReference != null && !packagesAndDependencies.ContainsKey(packageReference.PackageIdentity.Id))
                {
                    VersionRange range;
                    if (packageReference.HasAllowedVersions)
                    {
                        range = packageReference.AllowedVersions;
                    }
                    else
                    {
                        range = new VersionRange(packageReference.PackageIdentity.Version);
                    }

                    var stream = sourceRepository.GetNupkgStreamAsync(packageReference.PackageIdentity.Id, packageReference.PackageIdentity.Version, CancellationToken.None).Result;
                    if (stream != null)
                    {
                        try
                        {
                            var reader = new PackageArchiveReader(stream);
                            var dependency = new Packaging.Core.PackageDependency(packageReference.PackageIdentity.Id, range);
                            packagesAndDependencies.Add(packageReference.PackageIdentity.Id, Tuple.Create<PackageReaderBase, Packaging.Core.PackageDependency>(reader, dependency));
                        }
                        catch (Exception)
                        {
                            DisposePackageReaders(packagesAndDependencies);
                            stream.Dispose();

                            throw;
                        }
                    }
                    else
                    {
                        DisposePackageReaders(packagesAndDependencies);

                        var packageName = $"{packageReference.PackageIdentity.Id}.{packageReference.PackageIdentity.Version}";
                        throw new CommandLineException(NuGetResources.UnableToFindBuildOutput, $"{packageName}.nupkg");
                    }
                }
            }
        }

        private static void DisposePackageReaders(Dictionary<String, Tuple<PackageReaderBase, Packaging.Core.PackageDependency>> packagesAndDependencies)
        {
            // Release the open file handles
            foreach (var package in packagesAndDependencies)
            {
                package.Value.Item1.Dispose();
            }
        }

        private Configuration.ISettings ReadSettings(string solutionDirectory)
        {
                // Read the solution-level settings
                var solutionSettingsFile = Path.Combine(
                    solutionDirectory,
                    NuGetConstants.NuGetSolutionSettingsFolder);

                return Configuration.Settings.LoadDefaultSettings(
                    solutionSettingsFile,
                    configFileName: null,
                    machineWideSettings: MachineWideSettings);
        }

        private static void ProcessTransformFiles(Packaging.PackageBuilder builder, IEnumerable<Packaging.IPackageFile> transformFiles)
        {
            // Group transform by target file
            var transformGroups = transformFiles.GroupBy(file => RemoveExtension(file.Path), StringComparer.OrdinalIgnoreCase);
            var fileLookup = builder.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);

            foreach (var transformGroup in transformGroups)
            {
                Packaging.IPackageFile file;
                if (fileLookup.TryGetValue(transformGroup.Key, out file))
                {
                    // Replace the original file with a file that removes the transforms
                    builder.Files.Remove(file);
                    builder.Files.Add(new ReverseTransformFormFile(file, transformGroup));
                }
            }
        }

        /// <summary>
        /// Removes a file extension keeping the full path intact
        /// </summary>
        private static string RemoveExtension(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
        }

        private IEnumerable<Packaging.IPackageFile> GetTransformFiles(PackageReaderBase package)
        {
            var groups = package.GetContentItems();
            return groups.SelectMany(g => g.Items).Where(IsTransformFile).Select(f => new Packaging.PhysicalPackageFile() { TargetPath = f });
        }

        private static bool IsTransformFile(string file)
        {
            return Path.GetExtension(file).Equals(TransformFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private void AddSolutionDir()
        {
            // Add the solution dir to the list of properties
            string solutionDir = GetSolutionDir();

            // Add a path separator for Visual Studio macro compatibility
            solutionDir += Path.DirectorySeparatorChar;

            if (!String.IsNullOrEmpty(solutionDir))
            {
                if (ProjectProperties.ContainsKey("SolutionDir"))
                {
                    Logger.LogWarning(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("Warning_DuplicatePropertyKey"),
                            "SolutionDir"));
                }

                ProjectProperties["SolutionDir"] = solutionDir;
            }
        }

        private string GetSolutionDir()
        {
            return ProjectHelper.GetSolutionDir(_project.DirectoryPath);
        }

        private Packaging.Manifest ProcessNuspec(Packaging.PackageBuilder builder, string basePath)
        {
            string nuspecFile = GetNuspec();

            if (String.IsNullOrEmpty(nuspecFile))
            {
                return null;
            }

            Logger.LogMinimal(
                string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("UsingNuspecForMetadata"),
                    Path.GetFileName(nuspecFile)));

            using (Stream stream = File.OpenRead(nuspecFile))
            {
                // Don't validate the manifest since this might be a partial manifest
                // The bulk of the metadata might be coming from the project.
                Packaging.Manifest manifest = Packaging.Manifest.ReadFrom(stream, GetPropertyValue, validateSchema: true);
                builder.Populate(manifest.Metadata);

                if (manifest.Files != null)
                {
                    basePath = String.IsNullOrEmpty(basePath) ? Path.GetDirectoryName(nuspecFile) : basePath;
                    builder.PopulateFiles(basePath, manifest.Files);
                }

                return manifest;
            }
        }

        private string GetNuspec()
        {
            return GetNuspecPaths().FirstOrDefault(File.Exists);
        }

        private IEnumerable<string> GetNuspecPaths()
        {
            // Check for a nuspec in the project file
            yield return GetContentOrNone(file => Path.GetExtension(file).Equals(NuGetConstants.ManifestExtension, StringComparison.OrdinalIgnoreCase));
            // Check for a nuspec named after the project
            yield return Path.Combine(_project.DirectoryPath, Path.GetFileNameWithoutExtension(_project.FullPath) + NuGetConstants.ManifestExtension);
        }

        private string GetContentOrNone(Func<string, bool> matcher)
        {
            return GetFiles("Content").Concat(GetFiles("None")).FirstOrDefault(matcher);
        }

        private IEnumerable<string> GetFiles(string itemType)
        {
            foreach (dynamic item in _project.GetItems(itemType))
            {
                // the type of item is ProjectItem
                var fullPath = item.GetMetadataValue("FullPath") as string;
                yield return fullPath;
            }
        }

        private void AddFiles(Packaging.PackageBuilder builder, string itemType, string targetFolder)
        {
            // Skip files that are added by dependency packages
            ProjectManagement.FolderNuGetProject project = new ProjectManagement.FolderNuGetProject(_project.FullPath);
            var referencesTask = project.GetInstalledPackagesAsync(new CancellationToken());
            referencesTask.Wait();
            var references = referencesTask.Result;

            string projectName = Path.GetFileNameWithoutExtension(_project.FullPath);

            var contentFilesInDependencies = new List<FrameworkSpecificGroup>();
            if (references.Any())
            {
                contentFilesInDependencies = references
                    .Select(reference => new PackageArchiveReader(project.GetInstalledPackageFilePath(reference.PackageIdentity)))
                    .SelectMany(a => a.GetContentItems())
                    .ToList();
            }

            // Get the content files from the project
            foreach (var item in _project.GetItems(itemType))
            {
                string fullPath = item.GetMetadataValue("FullPath");
                if (_excludeFiles.Contains(Path.GetFileName(fullPath)))
                {
                    continue;
                }

                string targetFilePath = GetTargetPath(item);

                if (!File.Exists(fullPath))
                {
                    Logger.LogMinimal(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("Warning_FileDoesNotExist"),
                            targetFilePath));
                    continue;
                }

                // Skip target file paths containing msbuild variables since we do not offer a way to install files with variable paths.
                // These are show up in shared files found in universal apps.
                if (targetFilePath.IndexOf("$(MSBuild", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    Logger.LogWarning(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("Warning_UnresolvedFilePath"),
                            targetFilePath));
                    continue;
                }

                // if IncludeReferencedProjects is true and we are adding source files,
                // add projectName as part of the target to avoid file conflicts.
                string targetPath = IncludeReferencedProjects && itemType == SourcesItemType ?
                    Path.Combine(targetFolder, projectName, targetFilePath) :
                    Path.Combine(targetFolder, targetFilePath);

                // Check that file is added by dependency
                var targetFile = contentFilesInDependencies.SelectMany(f => f.Items).FirstOrDefault(a => a.Equals(targetPath, StringComparison.OrdinalIgnoreCase));
                if (targetFile != null)
                {
                    // Compare contents as well
                    var isEqual = ContentEquals(targetFile, fullPath);
                    if (isEqual)
                    {
                        Logger.LogMinimal(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                LocalizedResourceManager.GetString("PackageCommandFileFromDependencyIsNotChanged"),
                                targetFilePath));
                        continue;
                    }

                    Logger.LogMinimal(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("PackageCommandFileFromDependencyIsChanged"),
                            targetFilePath));
                }

                var packageFile = new Packaging.PhysicalPackageFile
                {
                    SourcePath = fullPath,
                    TargetPath = targetPath
                };
                AddFileToBuilder(builder, packageFile);
            }
        }

        private void AddFileToBuilder(Packaging.PackageBuilder builder, Packaging.PhysicalPackageFile packageFile)
        {
            if (!builder.Files.Any(p => packageFile.Path.Equals(p.Path, StringComparison.OrdinalIgnoreCase)))
            {
                WriteDetail(LocalizedResourceManager.GetString("AddFileToPackage"), packageFile.SourcePath, packageFile.TargetPath);
                builder.Files.Add(packageFile);
            }
            else
            {
                _logger.LogWarning(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("FileNotAddedToPackage"),
                        packageFile.SourcePath,
                        packageFile.TargetPath));
            }
        }

        private void WriteDetail(string format, params object[] args)
        {
            var console = _logger as Console;
            if (console != null && console.Verbosity == Verbosity.Detailed)
            {
                console.WriteLine(format, args);
            }
        }

        public static bool ContentEquals(string targetFile, string fullPath)
        {
            bool isEqual;
            using (var dependencyFileStream = File.OpenRead(targetFile))
            using (var fileContentStream = File.OpenRead(fullPath))
            {
                isEqual = dependencyFileStream.ContentEquals(fileContentStream);
            }
            return isEqual;
        }

        private string GetTargetPath(dynamic item)
        {
            string path = item.EvaluatedInclude;
            if (item.HasMetadata("Link"))
            {
                path = item.GetMetadataValue("Link");
            }
            return Normalize(path);
        }

        private string Normalize(string path)
        {
            string projectDirectoryPath = PathUtility.EnsureTrailingSlash(_project.DirectoryPath);
            string fullPath = PathUtility.GetAbsolutePath(projectDirectoryPath, path);

            // If the file is under the project root then remove the project root
            if (fullPath.StartsWith(projectDirectoryPath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(_project.DirectoryPath.Length).TrimStart(Path.DirectorySeparatorChar);
            }

            // Otherwise the file is probably a shortcut so just take the file name
            return Path.GetFileName(fullPath);
        }

        private class ReverseTransformFormFile : Packaging.IPackageFile
        {
            private readonly Lazy<Func<Stream>> _streamFactory;
            private readonly string _effectivePath;

            public ReverseTransformFormFile(Packaging.IPackageFile file, IEnumerable<Packaging.IPackageFile> transforms)
            {
                Path = file.Path + ".transform";
                _streamFactory = new Lazy<Func<Stream>>(() => ReverseTransform(file, transforms), isThreadSafe: false);
                TargetFramework = VersionUtility.ParseFrameworkNameFromFilePath(Path, out _effectivePath);
            }

            public string Path
            {
                get;
                private set;
            }

            public string EffectivePath
            {
                get
                {
                    return _effectivePath;
                }
            }

            public Stream GetStream()
            {
                return _streamFactory.Value();
            }

            [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "We need to return the MemoryStream for use.")]
            private static Func<Stream> ReverseTransform(Packaging.IPackageFile file, IEnumerable<Packaging.IPackageFile> transforms)
            {
                // Get the original
                XElement element = GetElement(file);

                // Remove all the transforms
                foreach (var transformFile in transforms)
                {
                    element.Except(GetElement(transformFile));
                }

                // Create the stream with the transformed content
                var ms = new MemoryStream();
                element.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);
                byte[] buffer = ms.ToArray();
                return () => new MemoryStream(buffer);
            }

            private static XElement GetElement(Packaging.IPackageFile file)
            {
                using (Stream stream = file.GetStream())
                {
                    return XElement.Load(stream);
                }
            }

            public FrameworkName TargetFramework
            {
                get;
                private set;
            }
        }
    }
}
