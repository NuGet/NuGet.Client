using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Commands.Rules;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class PackCommandRunner
    {
        public delegate IProjectFactory CreateProjectFactory(PackArgs packArgs, string path);

        private PackArgs _packArgs;
        internal static readonly string SymbolsExtension = ".symbols" + NuGetConstants.PackageExtension;
        private CreateProjectFactory _createProjectFactory;
        private const string Configuration = "configuration";

        private static readonly HashSet<string> _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            NuGetConstants.ManifestExtension,
            ".csproj",
            ".vbproj",
            ".fsproj",
            ".nproj",
            ".btproj",
            ".dxjsproj",
            ".xproj",
            ".json"
        };

        private static readonly string[] _defaultExcludes = new[] {
            // Exclude previous package files
            @"**\*".Replace('\\', Path.DirectorySeparatorChar) + NuGetConstants.PackageExtension,
            // Exclude all files and directories that begin with "."
            @"**\\.**".Replace('\\', Path.DirectorySeparatorChar), ".**"
        };

        // Target file paths to exclude when building the lib package for symbol server scenario
        private static readonly string[] _libPackageExcludes = new[] {
            @"**\*.pdb".Replace('\\', Path.DirectorySeparatorChar),
            @"src\**\*".Replace('\\', Path.DirectorySeparatorChar)
        };

        // Target file paths to exclude when building the symbols package for symbol server scenario
        private static readonly string[] _symbolPackageExcludes = new[] {
            @"content\**\*".Replace('\\', Path.DirectorySeparatorChar),
            @"tools\**\*.ps1".Replace('\\', Path.DirectorySeparatorChar)
        };

        private static readonly IReadOnlyList<string> defaultIncludeFlags = LibraryIncludeFlagUtils.NoContent.ToString().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private readonly HashSet<string> _excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<IPackageRule> Rules { get; set; }

        public PackCommandRunner(PackArgs packArgs, CreateProjectFactory createProjectFactory)
        {
            this._createProjectFactory = createProjectFactory;
            this._packArgs = packArgs;
            Rules = DefaultPackageRuleSet.Rules;
        }

        public void BuildPackage()
        {
            PackageArchiveReader package = BuildPackage(Path.GetFullPath(Path.Combine(_packArgs.CurrentDirectory, _packArgs.Path)));
        }

        private PackageArchiveReader BuildPackage(string path)
        {
            string extension = Path.GetExtension(path);

            if (ProjectJsonPathUtilities.IsProjectConfig(path))
            {
                return BuildFromProjectJson(path);
            }
            else if (extension.Equals(NuGetConstants.ManifestExtension, StringComparison.OrdinalIgnoreCase))
            {
                return BuildFromNuspec(path);
            }
            else
            {
                return BuildFromProjectFile(path);
            }
        }

        private PackageArchiveReader BuildFromProjectJson(string path)
        {
            PackageBuilder packageBuilder = CreatePackageBuilderFromProjectJson(path, _packArgs.GetPropertyValue);

            if (_packArgs.Build)
            {
                BuildProjectWithDotNet();
            }

            // Add output files
            AddOutputFiles(packageBuilder);

            if (_packArgs.Symbols)
            {
                // remove source related files when building the lib package
                ExcludeFilesForLibPackage(packageBuilder.Files);

                if (!packageBuilder.Files.Any())
                {
                    throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.Error_PackageCommandNoFilesForLibPackage, path, Strings.NuGetDocs));
                }
            }

            PackageArchiveReader package = BuildPackage(packageBuilder);

            if (_packArgs.Symbols)
            {
                BuildSymbolsPackage(path);
            }

            if (package != null && !_packArgs.NoPackageAnalysis)
            {
                AnalyzePackage(package, packageBuilder);
            }

            return package;
        }

        private void BuildProjectWithDotNet()
        {
            string properties = string.Empty;
            if (_packArgs.Properties.Any())
            {
                foreach (var property in _packArgs.Properties)
                {
                    if (property.Key.Equals(Configuration, StringComparison.OrdinalIgnoreCase))
                    {
                        properties = $"-c {property.Value}";
                    }
                }
            }

            if (!string.IsNullOrEmpty(_packArgs.OutputDirectory))
            {
                string outputDirectory = _packArgs.OutputDirectory;
                properties += $" -b \"{outputDirectory}\"";
            }

            string dotnetLocation = NuGetEnvironment.GetDotNetLocation();

            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = dotnetLocation,
                Arguments = $"build {properties}",
                WorkingDirectory = _packArgs.BasePath,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            int result;
            using (var process = Process.Start(processStartInfo))
            {
                process.WaitForExit();

                result = process.ExitCode;
            }

            if (0 != result)
            {
                // If the build fails, report the error
                throw new Exception(String.Format(CultureInfo.CurrentCulture, Strings.Error_BuildFailed, processStartInfo.FileName, processStartInfo.Arguments));
            }
        }

        private void AddOutputFiles(PackageBuilder builder)
        {
            // Get the target file path
            string outputDirectory;
            if (_packArgs.OutputDirectory != null)
            {
                outputDirectory = Path.Combine(_packArgs.OutputDirectory, builder.Id, "bin");
            }
            else
            {
                outputDirectory = _packArgs.OutputDirectory ?? Path.Combine(_packArgs.CurrentDirectory, "bin");
            }

            // Default to Debug unless the configuration was passed in as a property
            string configuration = "Debug";
            foreach (var property in _packArgs.Properties)
            {
                if (String.Equals(Configuration, property.Key, StringComparison.OrdinalIgnoreCase))
                {
                    configuration = property.Value;
                }
            }

            string targetPath = Path.Combine(outputDirectory, configuration, builder.Id + ".dll");
            NuGetFramework targetFramework = NuGetFramework.AnyFramework;

            // List of extensions to allow in the output path
            var allowedOutputExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".dll",
                ".exe",
                ".xml",
                ".winmd",
                ".runtimeconfig.json",

            };

            if (_packArgs.Symbols)
            {
                // Include pdbs for symbol packages
                allowedOutputExtensions.Add(".pdb");
            }

            string projectOutputDirectory = Path.GetDirectoryName(targetPath);

            string targetFileName = Path.GetFileNameWithoutExtension(targetPath);

            if (!Directory.Exists(projectOutputDirectory))
            {
                throw new Exception("No build found in " + projectOutputDirectory + ". Use the -Build option or build the project.");
            }

            foreach (var file in GetFiles(projectOutputDirectory, targetFileName, allowedOutputExtensions))
            {
                string targetFolder = Path.GetDirectoryName(file).Replace(projectOutputDirectory + Path.DirectorySeparatorChar, "");

                var packageFile = new PhysicalPackageFile
                {
                    SourcePath = file,
                    TargetPath = Path.Combine("lib", targetFolder, Path.GetFileName(file))
                };
                AddFileToBuilder(builder, packageFile);
            }
        }

        private void AddFileToBuilder(PackageBuilder builder, PhysicalPackageFile packageFile)
        {
            if (!builder.Files.Any(p => packageFile.Path.Equals(p.Path, StringComparison.OrdinalIgnoreCase)))
            {
                builder.Files.Add(packageFile);
            }
        }

        private static IEnumerable<string> GetFiles(string path, string fileNameWithoutExtension, HashSet<string> allowedExtensions)
        {
            return allowedExtensions.Select(extension => Directory.GetFiles(path, fileNameWithoutExtension + extension, SearchOption.AllDirectories)).SelectMany(a => a);
        }

        private PackageBuilder CreatePackageBuilderFromProjectJson(string path, Func<string, string> propertyProvider)
        {
            // Set the version property if the flag is set
            if (!String.IsNullOrEmpty(_packArgs.Version))
            {
                _packArgs.Properties["version"] = _packArgs.Version;
            }

            PackageBuilder builder = new PackageBuilder();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                LoadProjectJsonFile(builder, path, _packArgs.BasePath, Path.GetFileName(Path.GetDirectoryName(path)), stream, propertyProvider);
            }

            return builder;
        }

        public static bool ProcessProjectJsonFile(PackageBuilder builder, string basePath, string id, Func<string, string> propertyProvider)
        {
            if (basePath == null)
            {
                return false;
            }

            string path = ProjectJsonPathUtilities.GetProjectConfigPath(basePath, Path.GetFileName(basePath));
            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    LoadProjectJsonFile(builder, path, basePath, id, stream, propertyProvider);
                }
                return true;
            }

            return false;
        }

        private static void LoadProjectJsonFile(PackageBuilder builder, string path, string basePath, string id, Stream stream, Func<string, string> propertyProvider)
        {
            PackageSpec spec = JsonPackageSpecReader.GetPackageSpec(stream, id, path);

            if (id == null)
            {
                builder.Id = Path.GetFileName(basePath);
            }
            else
            {
                builder.Id = id;
            }
            if (!spec.IsDefaultVersion)
            {
                builder.Version = spec.Version;
            }
            if (spec.Title != null)
            {
                builder.Title = spec.Title;
            }
            if (spec.Description != null)
            {
                builder.Description = spec.Description;
            }
            if (spec.Copyright != null)
            {
                builder.Copyright = spec.Copyright;
            }
            if (spec.Authors.Any())
            {
                builder.Authors.AddRange(spec.Authors);
            }
            if (spec.Owners.Any())
            {
                builder.Owners.AddRange(spec.Owners);
            }
            Uri tempUri;
            if (Uri.TryCreate(spec.LicenseUrl, UriKind.Absolute, out tempUri))
            {
                builder.LicenseUrl = tempUri;
            }
            if (Uri.TryCreate(spec.ProjectUrl, UriKind.Absolute, out tempUri))
            {
                builder.ProjectUrl = tempUri;
            }
            if (Uri.TryCreate(spec.IconUrl, UriKind.Absolute, out tempUri))
            {
                builder.IconUrl = tempUri;
            }
            builder.RequireLicenseAcceptance = spec.RequireLicenseAcceptance;
            if (spec.Summary != null)
            {
                builder.Summary = spec.Summary;
            }
            if (spec.ReleaseNotes != null)
            {
                builder.ReleaseNotes = spec.ReleaseNotes;
            }
            if (spec.Language != null)
            {
                builder.Language = spec.Language;
            }

            foreach (var include in spec.PackInclude)
            {
                builder.AddFiles(basePath, include.Value, include.Key);
            }

            if (spec.Tags.Any())
            {
                builder.Tags.AddRange(spec.Tags);
            }

            if (spec.TargetFrameworks.Any())
            {
                foreach (var framework in spec.TargetFrameworks)
                {
                    if (framework.FrameworkName.IsUnsupported)
                    {
                        throw new Exception(String.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidTargetFramework, framework.FrameworkName));
                    }

                    AddDependencyGroups(framework.Dependencies.Concat(spec.Dependencies), framework.FrameworkName, builder);
                }
            }
            else
            {
                if (spec.Dependencies.Any())
                {
                    AddDependencyGroups(spec.Dependencies, NuGetFramework.AnyFramework, builder);
                }
            }
        }

        private static void AddDependencyGroups(IEnumerable<LibraryDependency> dependencies, NuGetFramework framework, PackageBuilder builder)
        {
            List<PackageDependency> packageDependencies = new List<PackageDependency>();

            foreach (var dependency in dependencies)
            {
                LibraryIncludeFlags effectiveInclude = dependency.IncludeType & ~dependency.SuppressParent;

                if (dependency.IncludeType == LibraryIncludeFlags.None || dependency.SuppressParent == LibraryIncludeFlags.All)
                {
                    continue;
                }

                if (dependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference)
                {
                    var reference = builder.FrameworkReferences.FirstOrDefault(r => r.AssemblyName == dependency.Name);
                    if (reference == null)
                    {
                        builder.FrameworkReferences.Add(new FrameworkAssemblyReference(dependency.Name, new NuGetFramework [] { framework }));
                    }
                    else
                    {
                        if (!reference.SupportedFrameworks.Contains(framework))
                        {
                            // Add another framework reference by replacing the existing reference
                            var newReference = new FrameworkAssemblyReference(reference.AssemblyName, reference.SupportedFrameworks.Concat(new NuGetFramework[] { framework }));
                            int index = builder.FrameworkReferences.IndexOf(reference);
                            builder.FrameworkReferences.Remove(reference);
                            builder.FrameworkReferences.Insert(index, newReference);
                        }
                    }
                }
                else
                {
                    List<string> includes = new List<string>();
                    List<string> excludes = new List<string>();
                    if (effectiveInclude == LibraryIncludeFlags.All)
                    {
                        includes.Add(LibraryIncludeFlags.All.ToString());
                    }
                    else if ((effectiveInclude & LibraryIncludeFlags.ContentFiles) == LibraryIncludeFlags.ContentFiles)
                    {
                        includes.AddRange(effectiveInclude.ToString().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    }
                    else
                    {
                        if ((LibraryIncludeFlagUtils.NoContent & ~effectiveInclude) != LibraryIncludeFlags.None)
                        {
                            excludes.AddRange((LibraryIncludeFlagUtils.NoContent & ~effectiveInclude).ToString().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
                        }
                    }

                    VersionRange version = dependency.LibraryRange.VersionRange;
                    if (!version.HasLowerBound && !version.HasUpperBound)
                    {
                        version = new VersionRange(builder.Version);
                    }

                    packageDependencies.Add(new PackageDependency(dependency.Name, version, includes, excludes));
                }
            }

            builder.DependencyGroups.Add(new PackageDependencyGroup(framework, packageDependencies));
        }

        private PackageArchiveReader BuildFromNuspec(string path)
        {
            PackageBuilder packageBuilder = CreatePackageBuilderFromNuspec(path);

            if (_packArgs.Symbols)
            {
                // remove source related files when building the lib package
                ExcludeFilesForLibPackage(packageBuilder.Files);

                if (!packageBuilder.Files.Any())
                {
                    throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.Error_PackageCommandNoFilesForLibPackage, path, Strings.NuGetDocs));
                }
            }

            PackageArchiveReader package = BuildPackage(packageBuilder);

            if (_packArgs.Symbols)
            {
                BuildSymbolsPackage(path);
            }

            if (package != null && !_packArgs.NoPackageAnalysis)
            {
                AnalyzePackage(package, packageBuilder);
            }

            return package;
        }

        private PackageBuilder CreatePackageBuilderFromNuspec(string path)
        {
            // Set the version property if the flag is set
            if (!String.IsNullOrEmpty(_packArgs.Version))
            {
                _packArgs.Properties["version"] = _packArgs.Version;
            }

            if (String.IsNullOrEmpty(_packArgs.BasePath))
            {
                return new PackageBuilder(path, _packArgs.GetPropertyValue, !_packArgs.ExcludeEmptyDirectories);
            }
            return new PackageBuilder(path, _packArgs.BasePath, _packArgs.GetPropertyValue, !_packArgs.ExcludeEmptyDirectories);
        }

        private PackageArchiveReader BuildFromProjectFile(string path)
        {
            if (String.IsNullOrEmpty(_packArgs.MsBuildDirectory.Value) || _createProjectFactory == null)
            {
                _packArgs.Logger.LogError(Strings.Error_CannotFindMsbuild);
                return null;
            }

            var factory = _createProjectFactory.Invoke(_packArgs, path);

            // Add the additional Properties to the properties of the Project Factory
            foreach (var property in _packArgs.Properties)
            {
                if (factory.GetProjectProperties().ContainsKey(property.Key))
                {
                    _packArgs.Logger.LogWarning(String.Format(CultureInfo.CurrentCulture, Strings.Warning_DuplicatePropertyKey, property.Key));
                }
                factory.GetProjectProperties()[property.Key] = property.Value;
            }

            // Create a builder for the main package as well as the sources/symbols package
            PackageBuilder mainPackageBuilder = factory.CreateBuilder(_packArgs.BasePath);

            if (mainPackageBuilder == null)
            {
                throw new Exception(String.Format(CultureInfo.CurrentCulture, Strings.Error_PackFailed, path));
            }

            // Build the main package
            PackageArchiveReader package = BuildPackage(mainPackageBuilder);

            if (package != null && !_packArgs.NoPackageAnalysis)
            {
                AnalyzePackage(package, mainPackageBuilder);
            }

            // If we're excluding symbols then do nothing else
            if (!_packArgs.Symbols)
            {
                return package;
            }

            WriteLine(String.Empty);
            WriteLine(Strings.Log_PackageCommandAttemptingToBuildSymbolsPackage, Path.GetFileName(path));

            factory.SetIncludeSymbols(true);
            PackageBuilder symbolsBuilder = factory.CreateBuilder(_packArgs.BasePath);
            symbolsBuilder.Version = mainPackageBuilder.Version;

            // Get the file name for the sources package and build it
            string outputPath = GetOutputPath(symbolsBuilder, symbols: true);
            BuildPackage(symbolsBuilder, outputPath);

            // this is the real package, not the symbol package
            return package;
        }

        private PackageArchiveReader BuildPackage(PackageBuilder builder, string outputPath = null)
        {
            if (!String.IsNullOrEmpty(_packArgs.Version))
            {
                builder.Version = new NuGetVersion(_packArgs.Version);
            }

            if (!string.IsNullOrEmpty(_packArgs.Suffix))
            {
                string version = VersionFormatter.Instance.Format("V", builder.Version, VersionFormatter.Instance);
                builder.Version = new NuGetVersion($"{version}-{_packArgs.Suffix}");
            }

            if (_packArgs.MinClientVersion != null)
            {
                builder.MinClientVersion = _packArgs.MinClientVersion;
            }

            outputPath = outputPath ?? GetOutputPath(builder, false, builder.Version);

            CheckForUnsupportedFrameworks(builder);

            ExcludeFiles(builder.Files);

            // Track if the package file was already present on disk
            bool isExistingPackage = File.Exists(outputPath);
            try
            {
                using (Stream stream = File.Create(outputPath))
                {
                    builder.Save(stream);
                }
            }
            catch
            {
                if (!isExistingPackage && File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
                throw;
            }

            if (_packArgs.LogLevel == LogLevel.Verbose)
            {
                PrintVerbose(outputPath, builder);
            }

            WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.Log_PackageCommandSuccess, outputPath));

            return new PackageArchiveReader(outputPath);
        }

        private void CheckForUnsupportedFrameworks(PackageBuilder builder)
        {
            foreach (var reference in builder.FrameworkReferences)
            {
                foreach (var framework in reference.SupportedFrameworks)
                {
                    if (framework.IsUnsupported)
                    {
                        throw new Exception(String.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidTargetFramework, reference.AssemblyName));
                    }
                }
            }
        }

        private void PrintVerbose(string outputPath, PackageBuilder builder)
        {
            WriteLine(String.Empty);
            var package = new PackageArchiveReader(outputPath);

            WriteLine("Id: {0}", builder.Id);
            WriteLine("Version: {0}", builder.Version);
            WriteLine("Authors: {0}", String.Join(", ", builder.Authors));
            WriteLine("Description: {0}", builder.Description);
            if (builder.LicenseUrl != null)
            {
                WriteLine("License Url: {0}", builder.LicenseUrl);
            }
            if (builder.ProjectUrl != null)
            {
                WriteLine("Project Url: {0}", builder.ProjectUrl);
            }
            if (builder.Tags.Any())
            {
                WriteLine("Tags: {0}", String.Join(", ", builder.Tags));
            }
            if (builder.DependencyGroups.Any())
            {
                WriteLine("Dependencies: {0}", String.Join(", ", builder.DependencyGroups.SelectMany(d => d.Packages).Select(d => d.ToString())));
            }
            else
            {
                WriteLine("Dependencies: None");
            }

            WriteLine(String.Empty);

            foreach (var file in package.GetFiles().OrderBy(p => p))
            {
                WriteLine(Strings.Log_PackageCommandAddedFile, file);
            }

            WriteLine(String.Empty);
        }

        internal void ExcludeFiles(ICollection<IPackageFile> packageFiles)
        {
            // Always exclude the nuspec file
            // Review: This exclusion should be done by the package builder because it knows which file would collide with the auto-generated
            // manifest file.
            var wildCards = _excludes.Concat(new[] { @"**\*" + NuGetConstants.ManifestExtension });
            if (!_packArgs.NoDefaultExcludes)
            {
                // The user has not explicitly disabled default filtering.
                wildCards = wildCards.Concat(_defaultExcludes);
            }
            wildCards = wildCards.Concat(_packArgs.Exclude);

            PathResolver.FilterPackageFiles(packageFiles, ResolvePath, wildCards);
        }

        private string ResolvePath(IPackageFile packageFile)
        {
            var physicalPackageFile = packageFile as PhysicalPackageFile;

            // For PhysicalPackageFiles, we want to filter by SourcePaths, the path on disk. The Path value maps to the TargetPath
            if (physicalPackageFile == null)
            {
                return packageFile.Path;
            }

            var path = physicalPackageFile.SourcePath;
            // Make sure that the basepath has a directory separator

            int index = path.IndexOf(PathUtility.EnsureTrailingSlash(_packArgs.BasePath), StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                // Since wildcards are going to be relative to the base path, remove the BasePath portion of the file's source path.
                // Also remove any leading path separator slashes
                path = path.Substring(index + _packArgs.BasePath.Length).TrimStart(Path.DirectorySeparatorChar);
            }

            return path;
        }

        private void BuildSymbolsPackage(string path)
        {
            PackageBuilder symbolsBuilder = CreatePackageBuilderFromNuspec(path);
            // remove unnecessary files when building the symbols package
            ExcludeFilesForSymbolPackage(symbolsBuilder.Files);

            if (!symbolsBuilder.Files.Any())
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.Error_PackageCommandNoFilesForSymbolsPackage, path, Strings.NuGetDocs));
            }

            string outputPath = GetOutputPath(symbolsBuilder, symbols: true);
            BuildPackage(symbolsBuilder, outputPath);
        }

        internal void AnalyzePackage(PackageArchiveReader package, PackageBuilder builder)
        {
            IEnumerable<IPackageRule> packageRules = Rules;
            IList<PackageIssue> issues = new List<PackageIssue>();
            NuGetVersion version;

            if (!NuGetVersion.TryParseStrict(package.GetIdentity().Version.ToString(), out version))
            {
                issues.Add(new PackageIssue(Strings.Warning_SemanticVersionTitle,
                    String.Format(CultureInfo.CurrentCulture, Strings.Warning_SemanticVersion, package.GetIdentity().Version),
                    Strings.Warning_SemanticVersionSolution));
            }

            foreach (var rule in packageRules)
            {
                issues.AddRange(rule.Validate(builder).OrderBy(p => p.Title, StringComparer.CurrentCulture));
            }

            if (issues.Count > 0)
            {
                _packArgs.Logger.LogWarning(
                    String.Format(CultureInfo.CurrentCulture, Strings.Warning_PackageCommandPackageIssueSummary, builder.Id));
                foreach (var issue in issues)
                {
                    PrintPackageIssue(issue);
                }
            }
        }

        private void PrintPackageIssue(PackageIssue issue)
        {
            WriteLine(String.Empty);
            _packArgs.Logger.LogWarning(String.Format(CultureInfo.CurrentCulture, Strings.Warning_PackageCommandIssueTitle, issue.Title));
            _packArgs.Logger.LogWarning(String.Format(CultureInfo.CurrentCulture, Strings.Warning_PackageCommandIssueDescription, issue.Description));

            if (!String.IsNullOrEmpty(issue.Solution))
            {
                _packArgs.Logger.LogWarning(String.Format(CultureInfo.CurrentCulture, Strings.Warning_PackageCommandIssueSolution, issue.Solution));
            }
        }

        internal static void ExcludeFilesForLibPackage(ICollection<IPackageFile> files)
        {
            PathResolver.FilterPackageFiles(files, file => file.Path, _libPackageExcludes);
        }

        internal static void ExcludeFilesForSymbolPackage(ICollection<IPackageFile> files)
        {
            PathResolver.FilterPackageFiles(files, file => file.Path, _symbolPackageExcludes);
        }

        private string GetOutputPath(PackageBuilder builder, bool symbols = false, NuGetVersion nugetVersion = null)
        {
            string version;

            if (nugetVersion != null)
            {
                version = nugetVersion.ToNormalizedString();
            }
            else
            {
                version = String.IsNullOrEmpty(_packArgs.Version) ? builder.Version.ToNormalizedString() : _packArgs.Version;
            }

            // Output file is {id}.{version}
            string outputFile = builder.Id + "." + version;

            // If this is a source package then add .symbols.nupkg to the package file name
            if (symbols)
            {
                outputFile += SymbolsExtension;
            }
            else
            {
                outputFile += NuGetConstants.PackageExtension;
            }

            string outputDirectory = _packArgs.OutputDirectory ?? _packArgs.CurrentDirectory;
            return Path.Combine(outputDirectory, outputFile);
        }

        public static void SetupCurrentDirectory(PackArgs packArgs)
        {
            string directory = Path.GetDirectoryName(packArgs.Path);

            if (!directory.Equals(packArgs.CurrentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(packArgs.OutputDirectory))
                {
                    packArgs.OutputDirectory = packArgs.CurrentDirectory;
                }

                packArgs.CurrentDirectory = directory;
                Directory.SetCurrentDirectory(packArgs.CurrentDirectory);
            }
        }

        public static string GetInputFile(PackArgs packArgs)
        {
            IEnumerable<string> files = packArgs.Arguments != null && packArgs.Arguments.Any() ? packArgs.Arguments : Directory.GetFiles(packArgs.CurrentDirectory);

            return GetInputFile(packArgs, files);
        }

        internal static string GetInputFile(PackArgs packArgs, IEnumerable<string> files)
        {
            if (files.Count() == 1 && Directory.Exists(files.First()))
            {
                string first = files.First();
                files = Directory.GetFiles(first);
            }

            var candidates = files.Where(file => _allowedExtensions.Contains(Path.GetExtension(file))).ToList();
            string result;

            candidates.RemoveAll(ext => ext.EndsWith(".lock.json", StringComparison.OrdinalIgnoreCase) ||
                                    (ext.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && 
                                    !Path.GetFileName(ext).Equals(ProjectJsonPathUtilities.ProjectConfigFileName, StringComparison.OrdinalIgnoreCase) &&
                                    !ext.EndsWith(ProjectJsonPathUtilities.ProjectConfigFileEnding, StringComparison.OrdinalIgnoreCase)));

            if (!candidates.Any())
            {
                throw new ArgumentException(Strings.InputFileNotSpecified);
            }
            if (candidates.Count == 1)
            {
                result = candidates[0];
            }
            else
            {
                // Remove all nuspec files
                candidates.RemoveAll(file => Path.GetExtension(file).Equals(NuGetConstants.ManifestExtension, StringComparison.OrdinalIgnoreCase));
                if (candidates.Count == 1)
                {
                    result = candidates[0];
                }
                else
                {
                    // Remove all json files
                    candidates.RemoveAll(file => Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase));
                    if (candidates.Count == 1)
                    {
                        result = candidates[0];
                    }
                    else
                    {
                        throw new ArgumentException(Strings.InputFileNotSpecified);
                    }
                }
            }

            return Path.Combine(packArgs.CurrentDirectory, result);
        }

        private void WriteLine(string message, object arg = null)
        {
            _packArgs.Logger.LogInformation(String.Format(CultureInfo.CurrentCulture, message, arg?.ToString()));
        }
    }
}
