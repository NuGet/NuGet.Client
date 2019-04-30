// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using NuGet;
using NuGet.CommandLine;

namespace SampleCommandLineExtensions
{
    [Export]
    [Command("beep", "Prints beep",
        UsageExample = "nuget beep",
        UsageSummary = "This command prints beep")]
    [DeprecatedCommand]
    public class BeepCommand : Command
    {
        public override void ExecuteCommand()
        {
            Console.WriteLine("Beep");
        }
    }
}
