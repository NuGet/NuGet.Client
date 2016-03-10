using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGet.Common;
using System.Globalization;

namespace NuGet.CommandLine.XPlat
{
    class PackCommand : Command
    {
        internal static readonly string SymbolsExtension = ".symbols" + NuGetConstants.PackageExtension;

        private string _currentDirectory;
        private Version _minClientVersionValue;
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
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string BasePath { get; set; }
        public bool ExcludeEmptyDirectories { get; set; }
        public string MinClientVersion { get; set; }
        public bool NoDefaultExcludes { get; set; }
        public bool NoPackageAnalysis { get; set; }
        public string OutputDirectory { get; set; }
        public string Suffix { get; set; }
        public bool Symbols { get; set; }
        public LogLevel LogLevel { get; set; }
        public string Version { get; set; }
        public Dictionary<string, string> Properties
        {
            get
            {
                return _properties;
            }
        }

        public PackCommand(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("pack", pack =>
            {
                pack.Description = Strings.PackCommand_Description;

                var basePath = pack.Option(
                    "-b|--basePath <basePath>",
                    Strings.BasePath_Description,
                    CommandOptionType.SingleValue);

                var excludeEmpty = pack.Option(
                    "-e|--excludeEmptyDirectories",
                    Strings.ExcludeEmptyDirectories_Description,
                    CommandOptionType.NoValue);

                var minClientVersion = pack.Option(
                    "--minClientVersion",
                    Strings.MinClientVersion_Description,
                    CommandOptionType.SingleValue);

                var noDefaultExcludes = pack.Option(
                    "--noDefaultExcludes",
                    Strings.NoDefaultExcludes_Description,
                    CommandOptionType.NoValue);

                var noPackageAnalysis = pack.Option(
                    "--noPackageAnalysis",
                    Strings.NoPackageAnalysis_Description,
                    CommandOptionType.NoValue);

                var outputDirectory = pack.Option(
                    "-o|--outputDirectory <outputDirectory>",
                    Strings.OutputDirectory_Description,
                    CommandOptionType.SingleValue);

                var suffix = pack.Option(
                    "--suffix",
                    Strings.Suffix_Description,
                    CommandOptionType.SingleValue);

                var symbols = pack.Option(
                    "-s|--symbols",
                    Strings.Symbols_Description,
                    CommandOptionType.NoValue);

                var verbosity = pack.Option(
                    "--verbosity",
                    Strings.Switch_Verbosity,
                    CommandOptionType.SingleValue);

                var versionOption = pack.Option(
                    "-v|--version",
                    Strings.Version_Description,
                    CommandOptionType.SingleValue);

                var arguments = pack.Argument(
                    "nuspec or project file",
                    Strings.InputFile_Description,
                    multipleValues: true);

                pack.OnExecute(() =>
                {
                    var logger = getLogger();

                    IEnumerable<string> files = arguments.Values.Any() ? arguments.Values : Directory.GetFiles(CurrentDirectory).ToList();
                    string path = GetInputFile(files);

                    Console.WriteLine("Attempting to build package from '{0}'", Path.GetFileName(path));

                    // If the BasePath is not specified, use the directory of the input file (nuspec / proj) file
                    BasePath = !basePath.HasValue() ? Path.GetDirectoryName(Path.GetFullPath(path)) : basePath.Value();

                    ExcludeEmptyDirectories = excludeEmpty.HasValue();
                    if (!String.IsNullOrEmpty(MinClientVersion))
                    {
                        if (!System.Version.TryParse(MinClientVersion, out _minClientVersionValue))
                        {
                            throw new ArgumentException(Strings.PackageCommandInvalidMinClientVersion);
                        }
                    }
                    NoDefaultExcludes = noDefaultExcludes.HasValue();
                    NoPackageAnalysis = noPackageAnalysis.HasValue();
                    OutputDirectory = outputDirectory.Value();
                    Suffix = suffix.Value();
                    Symbols = symbols.HasValue();
                    LogLevel = XPlatUtility.GetLogLevel(verbosity);
                    if (versionOption.HasValue())
                    {
                        Version version;
                        if (!System.Version.TryParse(versionOption.Value(), out version))
                        {
                            throw new ArgumentException("Invalid version");
                        }
                        Version = versionOption.Value();
                    }

                    PackageArchiveReader package = BuildPackage(Path.GetFullPath(Path.Combine(CurrentDirectory, path)));

                    if (package != null && !NoPackageAnalysis)
                    {
                        AnalyzePackage(package);
                    }
                    
                    return 0;
                });
            });
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

            if (Symbols)
            {
                // remove source related files when building the lib package
                ExcludeFilesForLibPackage(packageBuilder.Files);

                if (!packageBuilder.Files.Any())
                {
                    throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.PackageCommandNoFilesForLibPackage, path, Strings.NuGetDocs));
                }
            }

            PackageArchiveReader package = BuildPackage(packageBuilder);

            if (Symbols)
            {
                BuildSymbolsPackage(path);
            }

            return package;
        }

        private PackageBuilder CreatePackageBuilderFromNuspec(string path)
        {
            // Set the version property if the flag is set
            if (!String.IsNullOrEmpty(Version))
            {
                Properties["version"] = Version;
            }

            if (String.IsNullOrEmpty(BasePath))
            {
                return new PackageBuilder(path, GetPropertyValue, !ExcludeEmptyDirectories);
            }
            return new PackageBuilder(path, BasePath, GetPropertyValue, !ExcludeEmptyDirectories);
        }

        private PackageArchiveReader BuildFromProjectFile(string path)
        {
            /*
            var factory = new ProjectFactory(_msbuildDirectory.Value, path, Properties)
            {
                IsTool = Tool,
                Logger = Console,
                Build = Build,
                IncludeReferencedProjects = IncludeReferencedProjects
            };

            // Add the additional Properties to the properties of the Project Factory
            foreach (var property in Properties)
            {
                if (factory.ProjectProperties.ContainsKey(property.Key))
                {
                    Console.WriteWarning(LocalizedResourceManager.GetString("Warning_DuplicatePropertyKey"), property.Key);
                }
                factory.ProjectProperties[property.Key] = property.Value;
            }

            // Create a builder for the main package as well as the sources/symbols package
            PackageBuilder mainPackageBuilder = factory.CreateBuilder(BasePath);

            // Build the main package
            IPackage package = BuildPackage(mainPackageBuilder);

            // If we're excluding symbols then do nothing else
            if (!Symbols)
            {
                return package;
            }

            Console.WriteLine();
            Console.WriteLine(LocalizedResourceManager.GetString("PackageCommandAttemptingToBuildSymbolsPackage"), Path.GetFileName(path));

            factory.IncludeSymbols = true;
            PackageBuilder symbolsBuilder = factory.CreateBuilder(BasePath);
            symbolsBuilder.Version = mainPackageBuilder.Version;

            // Get the file name for the sources package and build it
            string outputPath = GetOutputPath(symbolsBuilder, symbols: true);
            BuildPackage(symbolsBuilder, outputPath);
            */

            // TODO - Fake package so the compile doesn't complain
            PackageArchiveReader package = new PackageArchiveReader(new MemoryStream());

            // this is the real package, not the symbol package
            return package;
        }

        private PackageArchiveReader BuildPackage(PackageBuilder builder, string outputPath = null)
        {
            if (!String.IsNullOrEmpty(Version))
            {
                builder.Version = new NuGetVersion(Version);
            }

            if (!string.IsNullOrEmpty(Suffix))
            {
                builder.Version = new NuGetVersion(builder.Version.Version, Suffix);
            }

            if (_minClientVersionValue != null)
            {
                builder.MinClientVersion = _minClientVersionValue;
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
                        
            if (LogLevel == LogLevel.Verbose)
            {
                PrintVerbose(outputPath, builder);
            }

            Console.WriteLine("Successfully created package '{0}'", outputPath);

            return new PackageArchiveReader(outputPath);
        }

        private void PrintVerbose(string outputPath, PackageBuilder builder)
        {
            Console.WriteLine();
            var package = new PackageArchiveReader(outputPath);

            Console.WriteLine("Id: {0}", builder.Id);
            Console.WriteLine("Version: {0}", builder.Version);
            Console.WriteLine("Authors: {0}", String.Join(", ", builder.Authors));
            Console.WriteLine("Description: {0}", builder.Description);
            if (builder.LicenseUrl != null)
            {
                Console.WriteLine("License Url: {0}", builder.LicenseUrl);
            }
            if (builder.ProjectUrl != null)
            {
                Console.WriteLine("Project Url: {0}", builder.ProjectUrl);
            }
            if (builder.Tags.Any())
            {
                Console.WriteLine("Tags: {0}", String.Join(", ", builder.Tags));
            }
            if (builder.DependencyGroups.Any())
            {
                Console.WriteLine("Dependencies: {0}", String.Join(", ", builder.DependencyGroups.SelectMany(d => d.Packages).Select(d => d.ToString())));
            }
            else
            {
                Console.WriteLine("Dependencies: None");
            }

            Console.WriteLine();

            foreach (var file in package.GetFiles().OrderBy(p => p))
            {
                Console.WriteLine(Strings.PackageCommandAddedFile, file);
            }

            Console.WriteLine();
        }

        internal void ExcludeFiles(ICollection<IPackageFile> packageFiles)
        {
            // Always exclude the nuspec file
            // Review: This exclusion should be done by the package builder because it knows which file would collide with the auto-generated
            // manifest file.
            var wildCards = _excludes.Concat(new[] { @"**\*" + NuGetConstants.ManifestExtension });
            if (!NoDefaultExcludes)
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

            int index = path.IndexOf(BasePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                // Since wildcards are going to be relative to the base path, remove the BasePath portion of the file's source path.
                // Also remove any leading path separator slashes
                path = path.Substring(index + BasePath.Length).TrimStart(Path.DirectorySeparatorChar);
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
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.PackageCommandNoFilesForSymbolsPackage, path, Strings.NuGetDocs));
            }

            string outputPath = GetOutputPath(symbolsBuilder, symbols: true);
            BuildPackage(symbolsBuilder, outputPath);
        }

        internal void AnalyzePackage(PackageArchiveReader package)
        {
            NuGetVersion version;
            if (!NuGetVersion.TryParseStrict(package.GetIdentity().Version.ToString(), out version))
            {
                WriteWarning(
                    String.Format(CultureInfo.CurrentCulture, Strings.PackageCommandPackageIssueSummary, package.GetIdentity().Id));
                PrintPackageIssue(Strings.Warning_SemanticVersionTitle,
                    String.Format(CultureInfo.CurrentCulture, Strings.Warning_SemanticVersion, package.GetIdentity().Version),
                    Strings.Warning_SemanticVersionSolution);
            }
        }

        private void PrintPackageIssue(string title, string description, string solution)
        {
            Console.WriteLine();
            WriteWarning(
                prependWarningText: false,
                value: Strings.PackageCommandIssueTitle,
                args: title);

            WriteWarning(
                prependWarningText: false,
                value: Strings.PackageCommandIssueDescription,
                args: description);

            if (!String.IsNullOrEmpty(solution))
            {
                WriteWarning(
                    prependWarningText: false,
                    value: Strings.PackageCommandIssueSolution,
                    args: solution);
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
            string version = String.IsNullOrEmpty(Version) ? builder.Version.ToString() : Version;

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

            string outputDirectory = OutputDirectory ?? CurrentDirectory;
            return Path.Combine(outputDirectory, outputFile);
        }

        private string GetPropertyValue(string propertyName)
        {
            string value;
            if (Properties.TryGetValue(propertyName, out value))
            {
                return value;
            }
            return null;
        }

        private string CurrentDirectory
        {
            get
            {
                return _currentDirectory ?? Directory.GetCurrentDirectory();
            }
            set
            {
                _currentDirectory = value;
            }
        }

        private string GetInputFile(IList<string> arguments)
        {
            IEnumerable<string> files = arguments.Any() ? arguments : Directory.GetFiles(CurrentDirectory);

            return GetInputFile(files);
        }

        internal string GetInputFile(IEnumerable<string> files)
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

            return Path.GetFullPath(Path.Combine(CurrentDirectory, result));
        }
    }
}
