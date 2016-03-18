using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Commands.Rules;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class PackCommandRunner
    {
        public delegate IProjectFactory CreateProjectFactory(PackArgs packArgs, string path);

        private PackArgs _packArgs;
        internal static readonly string SymbolsExtension = ".symbols" + NuGetConstants.PackageExtension;
        private CreateProjectFactory _createProjectFactory;


        private static readonly HashSet<string> _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            NuGetConstants.ManifestExtension,
            ".csproj",
            ".vbproj",
            ".fsproj",
            ".nproj",
            ".btproj",
            ".dxjsproj"
        };

        private static readonly string[] _defaultExcludes = new[] {
            // Exclude previous package files
            @"**\*" + NuGetConstants.PackageExtension,
            // Exclude all files and directories that begin with "."
            @"**\\.**", ".**"
        };

        // Target file paths to exclude when building the lib package for symbol server scenario
        private static readonly string[] _libPackageExcludes = new[] {
            @"**\*.pdb",
            @"src\**\*"
        };

        // Target file paths to exclude when building the symbols package for symbol server scenario
        private static readonly string[] _symbolPackageExcludes = new[] {
            @"content\**\*",
            @"tools\**\*.ps1"
        };

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

            if (extension.Equals(NuGetConstants.ManifestExtension, StringComparison.OrdinalIgnoreCase))
            {
                return BuildFromNuspec(path);
            }
            else
            {
                return BuildFromProjectFile(path);
            }
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
                builder.Version = new NuGetVersion(builder.Version.Version, _packArgs.Suffix);
            }

            if (_packArgs.MinClientVersion != null)
            {
                builder.MinClientVersion = _packArgs.MinClientVersion;
            }

            outputPath = outputPath ?? GetOutputPath(builder);

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

            int index = path.IndexOf(_packArgs.BasePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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

        private string GetOutputPath(PackageBuilder builder, bool symbols = false)
        {
            string version = String.IsNullOrEmpty(_packArgs.Version) ? builder.Version.ToString() : _packArgs.Version;

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

        public static string GetInputFile(PackArgs packArgs)
        {
            IEnumerable<string> files = packArgs.Arguments != null && packArgs.Arguments.Any() ? packArgs.Arguments : Directory.GetFiles(packArgs.CurrentDirectory);

            return GetInputFile(packArgs, files);
        }

        internal static string GetInputFile(PackArgs packArgs, IEnumerable<string> files)
        {
            var candidates = files.Where(file => _allowedExtensions.Contains(Path.GetExtension(file))).ToList();
            string result;
            switch (candidates.Count)
            {
                case 1:
                    result = candidates[0];
                    break;

                case 2:
                    // Remove all nuspec files
                    candidates.RemoveAll(file => Path.GetExtension(file).Equals(NuGetConstants.ManifestExtension, StringComparison.OrdinalIgnoreCase));
                    if (candidates.Count == 1)
                    {
                        result = candidates[0];
                    }
                    goto default;
                default:
                    throw new ArgumentException("Please specify a nuspec or project file to use");
            }

            return Path.GetFullPath(Path.Combine(packArgs.CurrentDirectory, result));
        }

        private void WriteLine(string message, object arg = null)
        {
            _packArgs.Logger.LogInformation(String.Format(CultureInfo.CurrentCulture, message, arg?.ToString()));
        }
    }
}
