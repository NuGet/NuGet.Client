using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.CommandLine.XPlat
{
    class PushCommand:Command
    {
        public PushCommand(CommandLineApplication app, Func<ILogger> getLogger)
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
                    var logger = getLogger();
                    
                    if (arguments.Values.Count < 1)
                    {
                        throw new ArgumentException(Strings.Push_MissingArguments);
                    }
                    var packagePath = arguments.Values[0];

                    if (!source.HasValue())
                    {
                        throw new ArgumentException(Strings.Error_MissingSourceParameter);
                    }

                    int t = 0;
                    if (timeout.HasValue())
                    {
                        if (!int.TryParse(timeout.Value(), out t))
                        {
                            throw new ArgumentException(Strings.Push_InvalidTimeout);
                        }
                    }

                    var setting = Settings.LoadDefaultSettings(Path.GetFullPath("."),
                                configFileName: null,
                                machineWideSettings: null);
                    var pushCommandResource = await GetPushCommandResource(source, setting);

                    await pushCommandResource.Push(packagePath,
                        t,
                        (s) => apikey.Value(),
                        logger);

                    return 0;
                });
            });
        }
    }
}
