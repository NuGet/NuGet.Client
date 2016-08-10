using System;
using System.Globalization;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Versioning;


namespace NuGet.CommandLine.XPlat
{
    internal static class LocalsCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("locals", locals =>
            {
                locals.Description = Strings.LocalsCommand_Description;

                locals.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var clear = locals.Option(
                    "-c|--clear <Arg>",
                    Strings.LocalsCommand_ClearDescription,
                    CommandOptionType.NoValue);

                var list = locals.Option(
                    "-l|--list",
                    Strings.LocalsCommand_ListDescription,
                    CommandOptionType.NoValue);

                var arguments = locals.Argument(
                    "Cache Location(s)",
                    Strings.LocalsCommand_ArgumentDescription,
                    multipleValues: false);

                locals.OnExecute(() =>
                {
                    var setting = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);

                    var localsCommandRunner = new LocalsCommandRunner(arguments.Values, setting, clear.HasValue(), list.HasValue());
                    localsCommandRunner.ExecuteCommand();

                    return 0;
                });
            });

        }
    }
}
