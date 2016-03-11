using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    using NuGet.Packaging;

    [Command(typeof(NuGetCommand), "pack", "PackageCommandDescription", MaxArgs = 1, UsageSummaryResourceName = "PackageCommandUsageSummary",
            UsageDescriptionResourceName = "PackageCommandUsageDescription", UsageExampleResourceName = "PackCommandUsageExamples")]
    public class PackCommand : Command
    {
        internal static readonly string SymbolsExtension = ".symbols" + Constants.PackageExtension;

        private static readonly string[] _defaultExcludes = new[] {
            // Exclude previous package files
            @"**\*" + Constants.PackageExtension,
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

        private static readonly HashSet<string> _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            Constants.ManifestExtension,
            ".csproj",
            ".vbproj",
            ".fsproj",
            ".nproj",
            ".btproj",
            ".dxjsproj"
        };

        private Version _minClientVersionValue;

        [Option(typeof(NuGetCommand), "PackageCommandOutputDirDescription")]
        public string OutputDirectory { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandBasePathDescription")]
        public string BasePath { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandVerboseDescription")]
        public bool Verbose { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandVersionDescription")]
        public string Version { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandSuffixDescription")]
        public string Suffix { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandExcludeDescription")]
        public ICollection<string> Exclude
        {
            get { return _excludes; }
        }

        [Option(typeof(NuGetCommand), "PackageCommandSymbolsDescription")]
        public bool Symbols { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandToolDescription")]
        public bool Tool { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandBuildDescription")]
        public bool Build { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandNoDefaultExcludes")]
        public bool NoDefaultExcludes { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandNoRunAnalysis")]
        public bool NoPackageAnalysis { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandExcludeEmptyDirectories")]
        public bool ExcludeEmptyDirectories { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandIncludeReferencedProjects")]
        public bool IncludeReferencedProjects { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandPropertiesDescription")]
        public Dictionary<string, string> Properties
        {
            get
            {
                return _properties;
            }
        }

        [Option(typeof(NuGetCommand), "PackageCommandMinClientVersion")]
        public string MinClientVersion { get; set; }

        [ImportMany]
        public IEnumerable<IPackageRule> Rules { get; set; }

        [Option(typeof(NuGetCommand), "CommandMSBuildVersion")]
        public string MSBuildVersion { get; set; }

        // TODO: Temporarily hide the real ConfigFile parameter from the help text.
        // When we fix #3230, we should remove this property.
        public new string ConfigFile { get; set; }

        public override void ExecuteCommand()
        {
            PackArgs packArgs = new PackArgs();
            packArgs.Logger = Console;

            // The directory that contains msbuild
            packArgs.MsBuildDirectory = new Lazy<string>(() => MsBuildUtility.GetMsbuildDirectory(MSBuildVersion, Console));

            if (Verbose)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Option_VerboseDeprecated"));
                Verbosity = Verbosity.Detailed;
            }

            // Get the input file
            string path = PackCommandRunner.GetInputFile(packArgs);

            Console.WriteLine(LocalizedResourceManager.GetString("PackageCommandAttemptingToBuildPackage"), Path.GetFileName(path));

            // If the BasePath is not specified, use the directory of the input file (nuspec / proj) file
            BasePath = String.IsNullOrEmpty(BasePath) ? Path.GetDirectoryName(Path.GetFullPath(path)) : BasePath;

            if (!String.IsNullOrEmpty(MinClientVersion))
            {
                if (!System.Version.TryParse(MinClientVersion, out _minClientVersionValue))
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("PackageCommandInvalidMinClientVersion"));
                }
            }

            packArgs.Arguments = Arguments;
            packArgs.BasePath = BasePath;
            packArgs.Build = Build;
            packArgs.ExcludeEmptyDirectories = ExcludeEmptyDirectories;
            packArgs.IncludeReferencedProjects = IncludeReferencedProjects;
            switch (Verbosity)
            {
                case Verbosity.Detailed:
                {
                    packArgs.LogLevel = Logging.LogLevel.Verbose;
                    break;
                }
                case Verbosity.Normal:
                {
                    packArgs.LogLevel = Logging.LogLevel.Information;
                    break;
                }
                case Verbosity.Quiet:
                {
                    packArgs.LogLevel = Logging.LogLevel.Minimal;
                    break;
                }
            }
            packArgs.MinClientVersion = _minClientVersionValue;
            packArgs.NoDefaultExcludes = NoDefaultExcludes;
            packArgs.NoPackageAnalysis = NoPackageAnalysis;
            packArgs.OutputDirectory = OutputDirectory;
            packArgs.Path = path;
            if (Properties.Any())
            {
                packArgs.Properties.AddRange(Properties);
            }
            packArgs.Suffix = Suffix;
            packArgs.Symbols = Symbols;
            packArgs.Tool = Tool;
            packArgs.Version = Version;

            PackCommandRunner packCommandRunner = new PackCommandRunner(packArgs);
            packCommandRunner.BuildPackage();
        }
   }
}