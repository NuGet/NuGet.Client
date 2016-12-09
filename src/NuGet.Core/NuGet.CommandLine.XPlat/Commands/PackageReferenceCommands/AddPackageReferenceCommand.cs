using System;
using System.Globalization;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    internal static class AddPackageReferenceCommand
    {
        private const string MSBuildExeName = "MSBuild.dll";

        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("addpkg", addPkgRef =>
            {
                addPkgRef.Description = "dotnet add pkg <package id> <package version>";
                addPkgRef.HelpOption(XPlatUtility.HelpOption);

                addPkgRef.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var id = addPkgRef.Option(
                    "--package",
                    "ID of the package",
                    CommandOptionType.SingleValue);

                var version = addPkgRef.Option(
                    "--version",
                    "Version of the package",
                    CommandOptionType.SingleValue);

                addPkgRef.OnExecute(() =>
                {
                    var logger = getLogger();
                    var settings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                    var dotnetPath = @"F:\paths\dotnet\dotnet.exe";
                    var projectPath = @"F:\validation\test\test.csproj";
                    ValidateArgument(id, "ID not given");
                    ValidateArgument(version, "Version not given");
                    logger.LogInformation("Starting copmmand");
                    var packageIdentity = new PackageIdentity(id.Values[0], new NuGetVersion(version.Values[0]));
                    var packageRefArgs = new PackageReferenceArgs(dotnetPath, projectPath, packageIdentity, settings, logger);
                    var addPackageRefCommandRunner = new AddPackageReferenceCommandRunner();
                    addPackageRefCommandRunner.ExecuteCommand(packageRefArgs);
                    return 0;
                });
            });
        }

        private static void ValidateArgument(CommandOption arg, string exceptionMessage)
        {
            if ((arg.Values.Count < 1) || string.IsNullOrWhiteSpace(arg.Values[0]))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, exceptionMessage));
            }
        }
    }
}