// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;

namespace NuGet.CommandLine.XPlat.Commands.Stage
{
    public static class StageCommand
    {
        internal static void Register(Command rootCommand, Func<ILoggerWithColor> getLogger)
        {
            var stageCommand = new DocumentedCommand(
                "stage",
                Strings.StageCommand_Description,
                "https://aka.ms/dotnet/nuget/stage");

            StagePushCommand.Register(stageCommand, getLogger);
            rootCommand.Subcommands.Add(stageCommand);
        }

        /// <summary>
        /// Adds the <c>stage</c> command to the supplied <c>dotnet nuget</c> command.
        /// </summary>
        /// <param name="rootCommand">The <c>dotnet nuget</c> command handler.</param>
        public static void GetStageCommand(Command rootCommand)
        {
            Register(rootCommand, () => CommandOutputLogger.Create());
        }
    }
}
