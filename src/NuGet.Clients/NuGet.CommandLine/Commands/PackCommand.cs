// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.CommandLine
{
    using System.Globalization;
    using NuGet.Packaging;
    using NuGet.Versioning;

    [Command(typeof(NuGetCommand), "pack", "PackageCommandDescription", MaxArgs = 1, UsageSummaryResourceName = "PackageCommandUsageSummary",
            UsageDescriptionResourceName = "PackageCommandUsageDescription", UsageExampleResourceName = "PackCommandUsageExamples")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class PackCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        internal static readonly string SymbolsExtension = ".symbols" + PackagingCoreConstants.NupkgExtension;

        private readonly HashSet<string> _excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Version _minClientVersionValue;

        [Option(typeof(NuGetCommand), "PackageCommandOutputDirDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string OutputDirectory { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandBasePathDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string BasePath { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandVersionDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Version { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandSuffixDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Suffix { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandExcludeDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ICollection<string> Exclude
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get { return _excludes; }
        }

        [Option(typeof(NuGetCommand), "PackageCommandSymbolsDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Symbols { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandToolDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Tool { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandBuildDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Build { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandNoDefaultExcludes")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NoDefaultExcludes { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandNoRunAnalysis")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NoPackageAnalysis { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandExcludeEmptyDirectories")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool ExcludeEmptyDirectories { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandIncludeReferencedProjects")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool IncludeReferencedProjects { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandPropertiesDescription", AltName = "p")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Dictionary<string, string> Properties
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get
            {
                return _properties;
            }
        }

        [Option(typeof(NuGetCommand), "PackageCommandMinClientVersion")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string MinClientVersion { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandSymbolPackageFormat")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string SymbolPackageFormat { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandPackagesDirectory")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string PackagesDirectory { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandSolutionDirectory")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string SolutionDirectory { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandMSBuildVersion")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string MSBuildVersion { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandMSBuildPath")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string MSBuildPath { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandInstallPackageToOutputPath")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool InstallPackageToOutputPath { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandOutputFileNamesWithoutVersion")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool OutputFileNamesWithoutVersion { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "PackageCommandConfigFile")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public new string ConfigFile { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void ExecuteCommand()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var packArgs = new PackArgs();
            packArgs.Logger = Console;
            packArgs.Arguments = Arguments;
            packArgs.OutputDirectory = OutputDirectory;
            packArgs.BasePath = BasePath;
            packArgs.MsBuildDirectory = new Lazy<string>(() => MsBuildUtility.GetMsBuildDirectoryFromMsBuildPath(MSBuildPath, MSBuildVersion, Console).Value.Path);

            if (!string.IsNullOrEmpty(PackagesDirectory))
            {
                packArgs.PackagesDirectory = Path.GetFullPath(PackagesDirectory);
            }

            if (!string.IsNullOrEmpty(SolutionDirectory))
            {
                packArgs.SolutionDirectory = Path.GetFullPath(SolutionDirectory);
            }

            // Get the input file
            packArgs.Path = PackCommandRunner.GetInputFile(packArgs);

            // Set the current directory if the files being packed are in a different directory
            PackCommandRunner.SetupCurrentDirectory(packArgs);

            Console.WriteLine(LocalizedResourceManager.GetString("PackageCommandAttemptingToBuildPackage"), Path.GetFileName(packArgs.Path));

            if (!string.IsNullOrEmpty(MinClientVersion))
            {
                if (!System.Version.TryParse(MinClientVersion, out _minClientVersionValue))
                {
                    throw new CommandException(LocalizedResourceManager.GetString("PackageCommandInvalidMinClientVersion"));
                }
            }

            if (!string.IsNullOrEmpty(SymbolPackageFormat))
            {
                packArgs.SymbolPackageFormat = PackArgs.GetSymbolPackageFormat(SymbolPackageFormat);
            }
            packArgs.Build = Build;
            packArgs.Exclude = Exclude;
            packArgs.ExcludeEmptyDirectories = ExcludeEmptyDirectories;
            packArgs.IncludeReferencedProjects = IncludeReferencedProjects;
            switch (Verbosity)
            {
                case Verbosity.Detailed:
                    {
                        packArgs.LogLevel = LogLevel.Verbose;
                        break;
                    }
                case Verbosity.Normal:
                    {
                        packArgs.LogLevel = LogLevel.Information;
                        break;
                    }
                case Verbosity.Quiet:
                    {
                        packArgs.LogLevel = LogLevel.Minimal;
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
            packArgs.InstallPackageToOutputPath = InstallPackageToOutputPath;
            packArgs.OutputFileNamesWithoutVersion = OutputFileNamesWithoutVersion;

            if (!string.IsNullOrEmpty(Version))
            {
                NuGetVersion version;
                if (!NuGetVersion.TryParse(Version, out version))
                {
                    throw new PackagingException(NuGetLogCode.NU5010, string.Format(CultureInfo.CurrentCulture, NuGetResources.InstallCommandPackageReferenceInvalidVersion, Version));
                }
                packArgs.Version = version.ToFullString();
            }

            var packCommandRunner = new PackCommandRunner(packArgs, ProjectFactory.ProjectCreator);
            if (!packCommandRunner.RunPackageBuild())
            {
                throw new ExitCodeException(1);
            }
        }
    }
}
