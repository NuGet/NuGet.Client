using System;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;


namespace NuGet.CommandLine.XPlat
{

    internal class CommandParsers
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            AddVerbParser.Register(app, getLogger);
            DisableVerbParser.Register(app, getLogger);
            EnableVerbParser.Register(app, getLogger);
            ListVerbParser.Register(app, getLogger);
            RemoveVerbParser.Register(app, getLogger);
            UpdateVerbParser.Register(app, getLogger);

        }
    }


}
