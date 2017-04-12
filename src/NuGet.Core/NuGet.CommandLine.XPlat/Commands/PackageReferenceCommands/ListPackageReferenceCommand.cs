using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{ 
    class ListPackageReferenceCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger,
        Func<IPackageReferenceCommandRunner> getCommandRunner)
        {
            app.Command("list", listPkg =>
            {
                listPkg.Description = Strings.ListPkg_Description;
                listPkg.HelpOption(XPlatUtility.HelpOption);

                listPkg.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var id = listPkg.Option(
                    "--package",
                    Strings.ListPkg_PackageIdDescription,
                    CommandOptionType.SingleValue);

                var projectPath = listPkg.Option(
                    "-p|--project",
                    Strings.ListPkg_ProjectPathDescription,
                    CommandOptionType.SingleValue);

                var frameworks = listPkg.Option(
                    "-f|--framework",
                    Strings.ListPkg_FrameworksDescription,
                    CommandOptionType.SingleValue);


                listPkg.OnExecute(() =>
                {                    
                    ValidateArgument(projectPath, listPkg.Name);
                    ValidateProjectPath(projectPath, listPkg.Name);

                    var logger = getLogger();

                    var packageReferenceArgs = new PackageReferenceArgs(projectPath.Value(), logger)
                    {
                        Frameworks = MSBuildStringUtility.Split(frameworks.Value()),
                    };

                    var msBuild = new MSBuildAPIUtility(logger);
                    var listPackageCommandRunner = getCommandRunner();
                    return listPackageCommandRunner.ExecuteCommand(packageReferenceArgs, msBuild);
                });
            });
        }

        private static void ValidateArgument(CommandOption arg, string commandName)
        {
            if (arg.Values.Count < 1)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    commandName,
                    arg.Template));
            }
        }

        private static void ValidateProjectPath(CommandOption projectPath, string commandName)
        {
            if (!File.Exists(projectPath.Value()) || !projectPath.Value().EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_PkgMissingOrInvalidProjectFile,
                    commandName,
                    projectPath.Value()));
            }
        }
    }
}
