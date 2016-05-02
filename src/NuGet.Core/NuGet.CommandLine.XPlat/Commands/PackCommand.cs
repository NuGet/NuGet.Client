using System;
using System.Globalization;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    internal static class PackCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("pack", pack =>
            {
                pack.Description = Strings.PackCommand_Description;

                pack.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var basePath = pack.Option(
                    "-b|--base-path <basePath>",
                    Strings.BasePath_Description,
                    CommandOptionType.SingleValue);

                var build = pack.Option(
                    "--build",
                    Strings.Build_Description,
                    CommandOptionType.NoValue);

                var exclude = pack.Option(
                    "--exclude",
                    Strings.Exclude_Description,
                    CommandOptionType.MultipleValue);

                var excludeEmpty = pack.Option(
                    "-e|--exclude-empty-directories",
                    Strings.ExcludeEmptyDirectories_Description,
                    CommandOptionType.NoValue);

                var minClientVersion = pack.Option(
                    "--min-client-version <version>",
                    Strings.MinClientVersion_Description,
                    CommandOptionType.SingleValue);

                var noDefaultExcludes = pack.Option(
                    "--no-default-excludes",
                    Strings.NoDefaultExcludes_Description,
                    CommandOptionType.NoValue);

                var noPackageAnalysis = pack.Option(
                    "--no-package-analysis",
                    Strings.NoPackageAnalysis_Description,
                    CommandOptionType.NoValue);

                var outputDirectory = pack.Option(
                    "-o|--output-directory <outputDirectory>",
                    Strings.OutputDirectory_Description,
                    CommandOptionType.SingleValue);

                var properties = pack.Option(
                    "-p|--properties <properties>",
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

                var verbosity = pack.Option(
                    "--verbosity <level>",
                    Strings.Switch_Verbosity,
                    CommandOptionType.SingleValue);

                var versionOption = pack.Option(
                    "-v|--version <version>",
                    Strings.Version_Description,
                    CommandOptionType.SingleValue);

                var arguments = pack.Argument(
                    "nuspec or project.json file",
                    Strings.InputFile_Description,
                    multipleValues: true);

                pack.OnExecute(() =>
                {
                    var logger = getLogger();
                    var packArgs = new PackArgs();
                    packArgs.Logger = logger;
                    packArgs.Arguments = arguments.Values;
                    packArgs.Path = PackCommandRunner.GetInputFile(packArgs);

                    // Set the current directory if the files being packed are in a different directory
                    PackCommandRunner.SetupCurrentDirectory(packArgs);

                    logger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.PackageCommandAttemptingToBuildPackage, Path.GetFileName(packArgs.Path)));

                    // If the BasePath is not specified, use the directory of the input file (nuspec / proj) file
                    packArgs.BasePath = !basePath.HasValue() ? Path.GetDirectoryName(Path.GetFullPath(packArgs.Path)) : basePath.Value();
                    packArgs.BasePath = packArgs.BasePath.TrimEnd(Path.DirectorySeparatorChar);

                    packArgs.Build = build.HasValue();
                    packArgs.Exclude = exclude.Values;
                    packArgs.ExcludeEmptyDirectories = excludeEmpty.HasValue();
                    packArgs.LogLevel = XPlatUtility.GetLogLevel(verbosity);

                    if (minClientVersion.HasValue())
                    {
                        Version version;
                        if (!Version.TryParse(minClientVersion.Value(), out version))
                        {
                            throw new ArgumentException(Strings.PackageCommandInvalidMinClientVersion);
                        }
                        packArgs.MinClientVersion = version;
                    }

                    packArgs.MachineWideSettings = new CommandLineXPlatMachineWideSetting();
                    packArgs.MsBuildDirectory = new Lazy<string>(() => string.Empty);
                    packArgs.NoDefaultExcludes = noDefaultExcludes.HasValue();
                    packArgs.NoPackageAnalysis = noPackageAnalysis.HasValue();
                    packArgs.OutputDirectory = outputDirectory.Value();

                    if (properties.HasValue())
                    {
                        foreach (var property in properties.Value().Split(';'))
                        {
                            int index = property.IndexOf('=');
                            if (index > 0 && index < property.Length - 1)
                            {
                                packArgs.Properties.Add(property.Substring(0, index), property.Substring(index + 1));
                            }
                        }
                    }

                    packArgs.Suffix = suffix.Value();
                    packArgs.Symbols = symbols.HasValue();
                    if (versionOption.HasValue())
                    {
                        NuGetVersion version;
                        if (!NuGetVersion.TryParse(versionOption.Value(), out version))
                        {
                            throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.PackageVersionInvalid, versionOption.Value()));
                        }
                        packArgs.Version = version.ToNormalizedString();
                    }

                    PackCommandRunner packCommandRunner = new PackCommandRunner(packArgs, null);
                    packCommandRunner.BuildPackage();

                    return 0;
                });
            });
        }
    }
}
