using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Logging;

namespace NuGet.CommandLine.XPlat
{
    class PackCommand : Command
    {
        public PackCommand(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("pack", pack =>
            {
                pack.Description = Strings.PackCommand_Description;

                var basePath = pack.Option(
                    "-b|--basePath <basePath>",
                    Strings.BasePath_Description,
                    CommandOptionType.SingleValue);

                var build = pack.Option(
                    "--build",
                    Strings.Build_Description,
                    CommandOptionType.NoValue);

                var excludeEmpty = pack.Option(
                    "-e|--excludeEmptyDirectories",
                    Strings.ExcludeEmptyDirectories_Description,
                    CommandOptionType.NoValue);

                var includeReferencedProjects = pack.Option(
                    "-e|--includeReferencedProjects",
                    Strings.IncludeReferencedProjects_Description,
                    CommandOptionType.NoValue);

                var minClientVersion = pack.Option(
                    "--minClientVersion <version>",
                    Strings.MinClientVersion_Description,
                    CommandOptionType.SingleValue);

                var msBuildVersion = pack.Option(
                    "--msBuildVersion <version>",
                    Strings.MsBuildVersion_Description,
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
                    "--suffix <suffix>",
                    Strings.Suffix_Description,
                    CommandOptionType.SingleValue);

                var symbols = pack.Option(
                    "-s|--symbols",
                    Strings.Symbols_Description,
                    CommandOptionType.NoValue);

                var tool = pack.Option(
                    "--tool",
                    Strings.Tool_Description,
                    CommandOptionType.NoValue);

                var verbosity = pack.Option(
                    "--verbosity <level>",
                    Strings.Switch_Verbosity,
                    CommandOptionType.SingleValue);

                var versionOption = pack.Option(
                    "-v|--version <version>",
                    Strings.Version_Description,
                    CommandOptionType.SingleValue);

                var arguments = pack.Argument(
                    "nuspec or project file",
                    Strings.InputFile_Description,
                    multipleValues: true);

                pack.OnExecute(() =>
                {
                    var logger = getLogger();
                    var packArgs = new PackArgs();
                    packArgs.Logger = logger;
                    packArgs.Arguments = arguments.Values;
                    packArgs.Path = PackCommandRunner.GetInputFile(packArgs);

                    logger.LogInformation(String.Format(CultureInfo.CurrentCulture, Strings.PackageCommandAttemptingToBuildPackage, Path.GetFileName(packArgs.Path)));

                    // If the BasePath is not specified, use the directory of the input file (nuspec / proj) file
                    packArgs.BasePath = !basePath.HasValue() ? Path.GetDirectoryName(Path.GetFullPath(packArgs.Path)) : basePath.Value();
                    packArgs.Build = build.HasValue();

                    packArgs.ExcludeEmptyDirectories = excludeEmpty.HasValue();
                    packArgs.LogLevel = XPlatUtility.GetLogLevel(verbosity);
                    if (minClientVersion.HasValue())
                    {
                        Version version;
                        if (!System.Version.TryParse(minClientVersion.Value(), out version))
                        {
                            throw new ArgumentException(Strings.PackageCommandInvalidMinClientVersion);
                        }
                        packArgs.MinClientVersion = version;
                    }

                    packArgs.MsBuildDirectory = new Lazy<string>(() => string.Empty);
                    packArgs.NoDefaultExcludes = noDefaultExcludes.HasValue();
                    packArgs.NoPackageAnalysis = noPackageAnalysis.HasValue();
                    packArgs.OutputDirectory = outputDirectory.Value();
                    packArgs.Suffix = suffix.Value();
                    packArgs.Symbols = symbols.HasValue();
                    packArgs.Tool = tool.HasValue();
                    if (versionOption.HasValue())
                    {
                        Version version;
                        if (!System.Version.TryParse(versionOption.Value(), out version))
                        {
                            throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.PackageVersionInvalid, versionOption.Value()));
                        }
                        packArgs.Version = versionOption.Value();
                    }

                    PackCommandRunner packCommandRunner = new PackCommandRunner(packArgs);
                    packCommandRunner.BuildPackage();

                    return 0;
                });
            });
        }
    }
}
