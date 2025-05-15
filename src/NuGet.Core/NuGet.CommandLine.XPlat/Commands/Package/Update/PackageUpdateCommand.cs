// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.CommandLine.XPlat.Commands.Package.Update
{
    internal static class PackageUpdateCommand
    {
        internal static void Register(Command rootCommand, Func<ILoggerWithColor> getLogger)
        {
            Register(rootCommand, getLogger, PackageUpdateCommandRunner.Run);
        }

        internal static void Register(Command rootCommand, Func<ILoggerWithColor> getLogger, Func<PackageUpdateArgs, ILoggerWithColor, IDGSpecFactory, MSBuildAPIUtility, CancellationToken, Task<int>> action)
        {
            var command = new DocumentedCommand("update", "updates packages in projects", "https://aka.ms/dotnet/package/update");

            var packagesArguments = new Argument<List<string>>("packages")
            {
                Arity = ArgumentArity.ZeroOrMore,
            };
            command.Arguments.Add(packagesArguments);

            var projectOption = new Option<string>("--project");
            command.Options.Add(projectOption);

            rootCommand.Subcommands.Add(command);
            command.SetAction(async (args, cancellationToken) =>
            {
                var logger = getLogger();
                var project = args.GetValue(projectOption) ?? Environment.CurrentDirectory;
                var packages = args.GetValue(packagesArguments);

                var commandArgs = new PackageUpdateArgs
                {
                    Project = project,
                    Packages = packages,
                };

                IDGSpecFactory dGSpecFactory = new DGSpecFactory();
                MSBuildAPIUtility mSBuild = new(logger);

                return await action(commandArgs, logger, dGSpecFactory, mSBuild, cancellationToken);
            });
        }
    }
}
