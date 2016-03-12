using System;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal static class PushCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("push", push =>
            {
                push.Description = "Push to remote source";

                var source = push.Option(
                    "-s|--source <source>",
                    Strings.Source_Description,
                    CommandOptionType.SingleValue);

                var timeout = push.Option(
                    "-t|--timeout <timeout>",
                    Strings.Push_Timeout_Description,
                    CommandOptionType.SingleValue);

                var apikey = push.Option(
                    "-k|--api-key <apiKey>",
                    Strings.ApiKey_Description,
                    CommandOptionType.SingleValue);

                var arguments = push.Argument(
                    "[root]",
                    Strings.Push_Package_ApiKey_Description,
                    multipleValues: true);

                push.OnExecute(async () =>
                {
                    if (arguments.Values.Count < 1)
                    {
                        throw new ArgumentException(Strings.Push_MissingArguments);
                    }

                    ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(), configFileName: null, machineWideSettings: null);
                    PackageSourceProvider provider = new PackageSourceProvider(settings);

                    string packagePath = arguments.Values[0];
                    string sourcePath = source.HasValue() ? source.Value() : provider.DefaultPushSource;

                    if (sourcePath == null)
                    {
                        throw new ArgumentException(Strings.Error_MissingSourceParameter);
                    }

                    int timeoutValue = 0;
                    if (timeout.HasValue())
                    {
                        if (!int.TryParse(timeout.Value(), out timeoutValue))
                        {
                            throw new ArgumentException(Strings.Push_InvalidTimeout);
                        }
                    }

                    PackageUpdateResource pushCommandResource = await CommandUtility.GetPushCommandResource(sourcePath, settings);
                    await pushCommandResource.Push(packagePath,
                        timeoutValue,
                        (s) => apikey.Value(),
                        getLogger());

                    return 0;
                });
            });
        }
    }
}
