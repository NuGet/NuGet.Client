// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace NuGet.CommandLine.XPlat.Commands.Stage
{
    internal static class StageCommand
    {
        internal static void Register(
            Command nugetCommand,
            Func<StagePushCommandArgs, Task<int>> action)
        {
            var stageCommand = new DocumentedCommand(
                "stage",
                Strings.StageCommand_Description,
                "https://aka.ms/dotnet/nuget/stage");

            StagePushCommand.Register(stageCommand, action);
            nugetCommand.Subcommands.Add(stageCommand);
        }
    }
}
