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
using System.Threading;

namespace NuGet.CommandLine.XPlat
{
    class DeleteCommand : Command
    {
        public DeleteCommand(CommandLineApplication app, Func<ILogger> getLogger)
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
                    var logger = getLogger();

                    if (arguments.Values.Count < 2)
                    {
                        throw new ArgumentException(Strings.Delete_MissingArguments);
                    }
                    var packageId = arguments.Values[0];
                    var packageVersion = arguments.Values[1];

                    if (!source.HasValue())
                    {
                        throw new ArgumentException(Strings.Error_MissingSourceParameter);
                    }

                    var setting = Settings.LoadDefaultSettings(Path.GetFullPath("."),
                                        configFileName: null,
                                        machineWideSettings: null);
                    var pushCommandResource = await GetPushCommandResource(source, setting);
                    await pushCommandResource.Delete(packageId,
                        packageVersion,
                        s => apikey.Value(),
                        (desc) => Confirm(nonInteractive.HasValue(), desc),
                        logger);
                    return 0;
                });
            });
        }
    }
}
