// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable

namespace NuGet.CommandLine.XPlat
{
    internal class ConfigPathsArgs
    {
        public string? WorkingDirectory { get; set; }
    }

    internal class ConfigSetArgs
    {
        public string? ConfigKey { get; set; }
        public string? ConfigValue { get; set; }
        public string? ConfigFile { get; set; }
    }
}
