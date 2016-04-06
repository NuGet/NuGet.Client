using System;
using System.Globalization;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    internal static class DeleteCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("delete", delete =>
            {
                delete.Description = Strings.Delete_Description;

                delete.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var source = delete.Option(
                    "-s|--source <source>",
                    Strings.Source_Description,
                    CommandOptionType.SingleValue);

                var nonInteractive = delete.Option(
                    "--non-interactive",
                    Strings.NonInteractive_Description,
                    CommandOptionType.NoValue);

                var apikey = delete.Option(
                    "-k|--api-key <apiKey>",
                    Strings.ApiKey_Description,
                    CommandOptionType.SingleValue);

                var arguments = delete.Argument(
                    "[root]",
                    Strings.Delete_PackageIdAndVersion_Description,
                    multipleValues: true);

                delete.OnExecute(async () =>
                {
                    if (arguments.Values.Count < 2)
                    {
                        throw new ArgumentException(Strings.Delete_MissingArguments);
                    }

                    string packageId = arguments.Values[0];
                    string packageVersion = arguments.Values[1];
                    string sourcePath = source.Value();
                    string apiKeyValue = apikey.Value();
                    bool nonInteractiveValue = nonInteractive.HasValue();

                    PackageSourceProvider sourceProvider = new PackageSourceProvider(XPlatUtility.CreateDefaultSettings());

                    await DeleteRunner.Run(
                        sourceProvider.Settings,
                        sourceProvider,
                        packageId,
                        packageVersion,
                        sourcePath,
                        apiKeyValue,
                        nonInteractiveValue,
                        Confirm,
                        getLogger());

                    return 0;
                });
            });
        }

        private static bool Confirm(string description)
        {
            ConsoleColor currentColor = ConsoleColor.Gray;
            try
            {
                currentColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.ConsoleConfirmMessage, description));
                var result = Console.ReadLine();
                return result.StartsWith(Strings.ConsoleConfirmMessageAccept, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.ForegroundColor = currentColor;
            }
        }
    }
}
