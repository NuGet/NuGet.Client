using System;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal static class DeleteCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("delete", delete =>
            {
                delete.Description = Strings.Delete_Description;

                var source = delete.Option(
                    "-s|--source <source>",
                    Strings.Source_Description,
                    CommandOptionType.SingleValue);

                var nonInteractive = delete.Option(
                    "--non-interactive <nonInteractive>",
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

                    if (!source.HasValue())
                    {
                        throw new ArgumentException(Strings.Error_MissingSourceParameter);
                    }

                    ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(), configFileName: null, machineWideSettings: null);
                    PackageUpdateResource pushCommandResource = await CommandUtility.GetPushCommandResource(source.Value(), settings);

                    await pushCommandResource.Delete(packageId,
                        packageVersion,
                        s => apikey.Value(),
                        (desc) => CommandUtility.Confirm(nonInteractive.HasValue(), desc),
                        getLogger());

                    return 0;
                });
            });
        }
    }
}
