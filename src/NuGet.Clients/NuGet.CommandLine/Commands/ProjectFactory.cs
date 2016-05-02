using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Versioning;

namespace NuGet.CommandLine
{
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Execution;
    using NuGet.Frameworks;
    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    using Console = System.Console;

    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    public class ProjectFactory : MSBuildUser, IProjectFactory
    {
        // Its type is Microsoft.Build.Evaluation.Project
        private dynamic _project;

        private Common.ILogger _logger;
        private Configuration.ISettings _settings;
        private bool _usingJsonFile;

        // Files we want to always exclude from the resulting package
        private static readonly HashSet<string> _excludeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            Constants.PackageReferenceFile,
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
            _settings = null;

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

        private Configuration.ISettings DefaultSettings
        {
            get
            {
                if (null == _settings)
                {
                    _settings = Configuration.Settings.LoadDefaultSettings(
                        _project.DirectoryPath,
                        null,
                        MachineWideSettings);
                }
                return _settings;
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
        public PackageBuilder CreateBuilder(string basePath)
        {
            BuildProject();

            if (!string.IsNullOrEmpty(TargetPath))
            {
                Logger.LogMinimal(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("PackagingFilesFromOutputPath"),
                        Path.GetFullPath(Path.GetDirectoryName(TargetPath))));
            }

            var builder = new PackageBuilder();

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
            Manifest manifest = null;

            // If there is a project.json file, load that and skip any nuspec that may exist
            if (!PackCommandRunner.ProcessProjectJsonFile(builder, basePath, builder.Id, GetPropertyValue))
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

        public string InitializeProperties(IPackageMetadata metadata)
        {
            // Set the properties that were resolved from the assembly/project so they can be
            // resolved by name if the nuspec contains tokens
            _properties.Clear();
            _properties.Add("Id", metadata.Id);
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
            if (!_properties.TryGetValue(propertyName, out value))
            {
                dynamic property = _project.GetProperty(propertyName);
                if (property != null)
                {
                    value = property.EvaluatedValue;
                }
            }

            return value;
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

        private object CreateLoggers()
        {
            var consoleLoggerType = _msbuildAssembly.GetType(
                "Microsoft.Build.Logging.ConsoleLogger",
                throwOnError: true);
            var consoleLogger = Activator.CreateInstance(
                consoleLoggerType);
            var verbosityProperty = consoleLoggerType.GetProperty("Verbosity");
            verbosityProperty.SetMethod.Invoke(consoleLogger, new object[] { Microsoft.Build.Framework.LoggerVerbosity.Quiet });

            var iloggerType = _frameworkAssembly.GetType(
                "Microsoft.Build.Framework.ILogger",
                throwOnError: true);
            var loggerList = typeof(List<>)
                .MakeGenericType(iloggerType)
                .GetConstructor(Type.EmptyTypes)
                .Invoke(null);
            var addMethod = loggerList.GetType().GetMethod("Add");
            addMethod.Invoke(loggerList, new[] { consoleLogger });

            return loggerList;
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

        private void ExtractMetadataFromProject(PackageBuilder builder)
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
            var nuspecFile = Path.ChangeExtension(projectFileFullName, Constants.ManifestExtension);
            return File.Exists(nuspecFile);
        }

        /// <summary>
        /// Adds referenced projects that have corresponding nuspec files as dependencies.
        /// </summary>
        /// <param name="dependencies">The dependencies collection where the new dependencies
        /// are added into.</param>
        private void AddProjectReferenceDependencies(Dictionary<string, PackageDependency> dependencies)
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

        private bool ProcessJsonFile(PackageBuilder builder, string basePath, string id)
        {
            return PackCommandRunner.ProcessProjectJsonFile(builder, basePath, id, GetPropertyValue);
        }

        // Creates a package dependency from the given project, which has a corresponding
        // nuspec file.
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to continue regardless of any error we encounter extracting metadata.")]
        private PackageDependency CreateDependencyFromProject(dynamic project, Dictionary<string, PackageDependency> dependencies)
        {
            try
            {
                var projectFactory = new ProjectFactory(_msbuildDirectory, _msbuildAssembly, _frameworkAssembly, project);
                projectFactory.Build = Build;
                projectFactory.ProjectProperties = ProjectProperties;
                projectFactory.BuildProject();
                var builder = new PackageBuilder();

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

                return new PackageDependency(
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

        private void AddOutputFiles(PackageBuilder builder)
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
                var packageFile = new PhysicalPackageFile
                {
                    SourcePath = file,
                    TargetPath = Path.Combine(targetFolder, Path.GetFileName(file))
                };
                AddFileToBuilder(builder, packageFile);
            }
        }

        private void ProcessDependencies(PackageBuilder builder)
        {
            // get all packages and dependencies, including the ones in project references
            var packagesAndDependencies = new Dictionary<String, Tuple<IPackage, NuGet.PackageDependency>>();
            ApplyAction(p => p.AddDependencies(packagesAndDependencies));

            // list of all dependency packages
            var packages = packagesAndDependencies.Values.Select(t => t.Item1).ToList();

            // Add the transform file to the package builder
            ProcessTransformFiles(builder, packages.SelectMany(GetTransformFiles));

            var dependencies = new Dictionary<string, PackageDependency>();
            if (!_usingJsonFile)
            {
                dependencies = builder.DependencyGroups.SelectMany(d => d.Packages)
                .ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
            }

            // Reduce the set of packages we want to include as dependencies to the minimal set.
            // Normally, packages.config has the full closure included, we only add top level
            // packages, i.e. packages with in-degree 0
            foreach (var package in GetMinimumSet(packages))
            {
                // Don't add duplicate dependencies
                if (dependencies.ContainsKey(package.Id))
                {
                    continue;
                }

                var dependency = packagesAndDependencies[package.Id].Item2;
                dependencies[dependency.Id] = new PackageDependency(dependency.Id, VersionRange.Parse(dependency.VersionSpec.ToString()));
            }

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
                            List<PackageDependency> newPackagesList = new List<PackageDependency>(group.Packages);
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

        private void AddDependencies(Dictionary<String, Tuple<IPackage, NuGet.PackageDependency>> packagesAndDependencies)
        {
            PackageReferenceFile file = PackageReferenceFile.CreateFromProject(_project.FullPath);
            if (!File.Exists(file.FullPath))
            {
                return;
            }
            Logger.LogMinimal(LocalizedResourceManager.GetString("UsingPackagesConfigForDependencies"));

            // Get the solution repository
            IPackageRepository repository = GetPackagesRepository();

            // Collect all packages
            IDictionary<PackageName, NuGet.PackageReference> packageReferences =
                file.GetPackageReferences()
                .Where(r => !r.IsDevelopmentDependency)
                .ToDictionary(r => new PackageName(r.Id, r.Version));
            // add all packages and create an associated dependency to the dictionary
            foreach (NuGet.PackageReference reference in packageReferences.Values)
            {
                if (repository != null)
                {
                    IPackage package = repository.FindPackage(reference.Id, reference.Version);
                    if (package != null && !packagesAndDependencies.ContainsKey(package.Id))
                    {
                        NuGet.IVersionSpec spec = GetVersionConstraint(packageReferences, package);
                        var dependency = new NuGet.PackageDependency(package.Id, spec);
                        packagesAndDependencies.Add(package.Id, new Tuple<IPackage, NuGet.PackageDependency>(package, dependency));
                    }
                }
            }
        }

        private static NuGet.IVersionSpec GetVersionConstraint(IDictionary<PackageName, NuGet.PackageReference> packageReferences, IPackage package)
        {
            NuGet.IVersionSpec defaultVersionConstraint = NuGet.VersionUtility.ParseVersionSpec(package.Version.ToString());

            NuGet.PackageReference packageReference;
            var key = new PackageName(package.Id, package.Version);
            if (!packageReferences.TryGetValue(key, out packageReference))
            {
                return defaultVersionConstraint;
            }

            return packageReference.VersionConstraint ?? defaultVersionConstraint;
        }

        private IEnumerable<IPackage> GetMinimumSet(List<IPackage> packages)
        {
            return new Walker(packages, TargetFramework).GetMinimalSet();
        }

        private static void ProcessTransformFiles(PackageBuilder builder, IEnumerable<NuGet.IPackageFile> transformFiles)
        {
            // Group transform by target file
            var transformGroups = transformFiles.GroupBy(file => RemoveExtension(file.Path), StringComparer.OrdinalIgnoreCase);
            var fileLookup = builder.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);

            foreach (var transformGroup in transformGroups)
            {
                IPackageFile file;
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

        private IEnumerable<NuGet.IPackageFile> GetTransformFiles(IPackage package)
        {
            return package.GetContentFiles().Where(IsTransformFile);
        }

        private static bool IsTransformFile(NuGet.IPackageFile file)
        {
            return Path.GetExtension(file.Path).Equals(TransformFileExtension, StringComparison.OrdinalIgnoreCase);
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

        private IPackageRepository GetPackagesRepository()
        {
            string solutionDir = GetSolutionDir();
            string defaultValue = SettingsUtility.GetRepositoryPath(DefaultSettings);

            string target = null;
            if (!String.IsNullOrEmpty(solutionDir))
            {
                string configValue = GetPackagesPath(solutionDir);
                // solution dir exists, no default packages folder specified anywhere,
                // default to hardcoded "packages" folder under solution
                if (string.IsNullOrEmpty(configValue) && string.IsNullOrEmpty(defaultValue))
                {
                    configValue = PackagesFolder;
                }

                if (!string.IsNullOrEmpty(configValue))
                {
                    target = Path.Combine(solutionDir, configValue);
                }
            }

            if (string.IsNullOrEmpty(target))
            {
                target = defaultValue;
            }

            if (!string.IsNullOrEmpty(target) && Directory.Exists(target))
            {
                return new SharedPackageRepository(target);
            }

            return null;
        }

        private static string GetPackagesPath(string dir)
        {
            string configPath = Path.Combine(dir, Configuration.Settings.DefaultSettingsFileName);

            try
            {
                // Support the hidden feature
                if (File.Exists(configPath))
                {
                    using (Stream stream = File.OpenRead(configPath))
                    {
                        // It's possible for the repositoryPath element to be missing in older versions of
                        // a NuGet.config file.
                        var repositoryPathElement = XmlUtility.LoadSafe(stream).Root.Element("repositoryPath");
                        if (repositoryPathElement != null)
                        {
                            return repositoryPathElement.Value;
                        }
                    }
                }
            }
            catch (FileNotFoundException)
            {
            }

            return null;
        }

        private Manifest ProcessNuspec(PackageBuilder builder, string basePath)
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
                Manifest manifest = Manifest.ReadFrom(stream, GetPropertyValue, validateSchema: true);
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
            yield return GetContentOrNone(file => Path.GetExtension(file).Equals(Constants.ManifestExtension, StringComparison.OrdinalIgnoreCase));
            // Check for a nuspec named after the project
            yield return Path.Combine(_project.DirectoryPath, Path.GetFileNameWithoutExtension(_project.FullPath) + Constants.ManifestExtension);
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

        private void AddFiles(PackageBuilder builder, string itemType, string targetFolder)
        {
            // Skip files that are added by dependency packages
            PackageReferenceFile packageReferenceFile = PackageReferenceFile.CreateFromProject(_project.FullPath);
            var references = packageReferenceFile.GetPackageReferences();
            IPackageRepository repository = GetPackagesRepository();
            string projectName = Path.GetFileNameWithoutExtension(_project.FullPath);

            var contentFilesInDependencies = new List<NuGet.IPackageFile>();
            if (references.Any() && repository != null)
            {
                contentFilesInDependencies = references
                    .Select(reference => repository.FindPackage(reference.Id, reference.Version))
                    .Where(a => a != null)
                    .SelectMany(a => a.GetContentFiles())
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
                NuGet.IPackageFile targetFile = contentFilesInDependencies.Find(a => a.Path.Equals(targetPath, StringComparison.OrdinalIgnoreCase));
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

                var packageFile = new PhysicalPackageFile
                {
                    SourcePath = fullPath,
                    TargetPath = targetPath
                };
                AddFileToBuilder(builder, packageFile);
            }
        }

        private void AddFileToBuilder(PackageBuilder builder, PhysicalPackageFile packageFile)
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
            var console = _logger as NuGet.Common.Console;
            if (console != null && console.Verbosity == Verbosity.Detailed)
            {
                console.WriteLine(format, args);
            }
        }

        public static bool ContentEquals(NuGet.IPackageFile targetFile, string fullPath)
        {
            bool isEqual;
            using (var dependencyFileStream = targetFile.GetStream())
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

        private class Walker : PackageWalker
        {
            private readonly IPackageRepository _repository;
            private readonly List<IPackage> _packages;

            public Walker(List<IPackage> packages, FrameworkName targetFramework) :
                base(targetFramework)
            {
                _packages = packages;
                _repository = new ReadOnlyPackageRepository(packages.ToList());
            }

            protected override bool SkipDependencyResolveError
            {
                get
                {
                    // For the pack command, when don't need to throw if a dependency is missing
                    // from a nuspec file.
                    return true;
                }
            }

            protected override IPackage ResolveDependency(NuGet.PackageDependency dependency)
            {
                return _repository.ResolveDependency(dependency, allowPrereleaseVersions: false, preferListedPackages: false);
            }

            protected override bool OnAfterResolveDependency(IPackage package, IPackage dependency)
            {
                _packages.Remove(dependency);
                return base.OnAfterResolveDependency(package, dependency);
            }

            public IEnumerable<IPackage> GetMinimalSet()
            {
                foreach (var package in _repository.GetPackages())
                {
                    Walk(package);
                }
                return _packages;
            }
        }

        private class ReverseTransformFormFile : IPackageFile
        {
            private readonly Lazy<Func<Stream>> _streamFactory;
            private readonly string _effectivePath;

            public ReverseTransformFormFile(IPackageFile file, IEnumerable<NuGet.IPackageFile> transforms)
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
            private static Func<Stream> ReverseTransform(IPackageFile file, IEnumerable<NuGet.IPackageFile> transforms)
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

            private static XElement GetElement(IPackageFile file)
            {
                using (Stream stream = file.GetStream())
                {
                    return XElement.Load(stream);
                }
            }

            private static XElement GetElement(NuGet.IPackageFile file)
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