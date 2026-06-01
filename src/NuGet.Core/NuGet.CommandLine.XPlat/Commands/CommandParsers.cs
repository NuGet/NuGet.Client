// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Microsoft.Extensions.CommandLineUtils;

namespace NuGet.CommandLine.XPlat
{
    internal static class CommandParsers
    {
        // Registers placeholders on the legacy CommandLineApplication so that `dotnet nuget --help`
        // still lists these verbs. They are implemented with System.CommandLine (see Verbs.cs).
        public static void Register(CommandLineApplication app)
        {
            AddVerbParser.Register(app);
            DisableVerbParser.Register(app);
            EnableVerbParser.Register(app);
            ListVerbParser.Register(app);
            RemoveVerbParser.Register(app);
            UpdateVerbParser.Register(app);
        }
    }
}
