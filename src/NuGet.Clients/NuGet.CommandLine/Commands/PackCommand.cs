using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    using System.Globalization;
    using NuGet.Packaging;
    using NuGet.Versioning;

    [Command(typeof(NuGetCommand), "pack", "PackageCommandDescription", MaxArgs = 1, UsageSummaryResourceName = "PackageCommandUsageSummary",
            UsageDescriptionResourceName = "PackageCommandUsageDescription", UsageExampleResourceName = "PackCommandUsageExamples")]
    public class PackCommand : Command
    {
        internal static readonly string SymbolsExtension = ".symbols" + Constants.PackageExtension;

        private readonly HashSet<string> _excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Version _minClientVersionValue;

        [Option(typeof(NuGetCommand), "PackageCommandOutputDirDescription")]
        public string OutputDirectory { get; set; }

        [Option(typeof(NuGetCommand), "PackageCommandBasePathDescription")]
        public string BasePath { get; set; }

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

        [Option(typeof(NuGetCommand), "CommandMSBuildVersion")]
        public string MSBuildVersion { get; set; }

        [Option(typeof(NuGetCommand), "CommandMSBuildPath")]
        public string MSBuildPath { get; set; }

        // TODO: Temporarily hide the real ConfigFile parameter from the help text.
        // When we fix #3230, we should remove this property.
        public new string ConfigFile { get; set; }

        public override void ExecuteCommand()
        {
            PackArgs packArgs = new PackArgs();
            packArgs.Logger = Console;
            packArgs.Arguments = Arguments;
            packArgs.OutputDirectory = OutputDirectory;
            packArgs.BasePath = BasePath;
            packArgs.MsBuildDirectory = MsBuildUtility.GetMsBuildDirectoryFromMsBuildPath(MSBuildPath, MSBuildVersion, Console);

            // Get the input file
            packArgs.Path = PackCommandRunner.GetInputFile(packArgs);

            // Set the current directory if the files being packed are in a different directory
            PackCommandRunner.SetupCurrentDirectory(packArgs);

            Console.WriteLine(LocalizedResourceManager.GetString("PackageCommandAttemptingToBuildPackage"), Path.GetFileName(packArgs.Path));

            if (!String.IsNullOrEmpty(MinClientVersion))
            {
                if (!System.Version.TryParse(MinClientVersion, out _minClientVersionValue))
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("PackageCommandInvalidMinClientVersion"));
                }
            }

            packArgs.Build = Build;
            packArgs.Exclude = Exclude;
            packArgs.ExcludeEmptyDirectories = ExcludeEmptyDirectories;
            packArgs.IncludeReferencedProjects = IncludeReferencedProjects;
            switch (Verbosity)
            {
                case Verbosity.Detailed:
                {
                    packArgs.LogLevel = Common.LogLevel.Verbose;
                    break;
                }
                case Verbosity.Normal:
                {
                    packArgs.LogLevel = Common.LogLevel.Information;
                    break;
                }
                case Verbosity.Quiet:
                {
                    packArgs.LogLevel = Common.LogLevel.Minimal;
                    break;
                }
            }
            packArgs.MinClientVersion = _minClientVersionValue;
            packArgs.NoDefaultExcludes = NoDefaultExcludes;
            packArgs.NoPackageAnalysis = NoPackageAnalysis;
            if (Properties.Any())
            {
                packArgs.Properties.AddRange(Properties);
            }
            packArgs.Suffix = Suffix;
            packArgs.Symbols = Symbols;
            packArgs.Tool = Tool;

            if (!string.IsNullOrEmpty(Version))
            {
                NuGetVersion version;
                if (!NuGetVersion.TryParse(Version, out version))
                {
                    throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, NuGetResources.InstallCommandPackageReferenceInvalidVersion, Version));
                }
                packArgs.Version = version.ToNormalizedString();
            }

            PackCommandRunner packCommandRunner = new PackCommandRunner(packArgs, ProjectFactory.ProjectCreator);
            packCommandRunner.BuildPackage();
        }
   }
}