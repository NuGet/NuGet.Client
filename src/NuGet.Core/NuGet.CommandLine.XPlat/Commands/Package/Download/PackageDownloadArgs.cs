// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands.Package.PackageDownload;

internal class PackageDownloadArgs
{
    public IReadOnlyList<Package> Packages { get; set; }
    public IList<string> Sources { get; set; }
    public string OutputDirectory { get; set; }
    public string ConfigFile { get; set; }

    public bool IncludePrerelease { get; set; }
    public bool DownloadOnly { get; set; }
    public bool AllowInsecureConnections { get; set; }
    public bool Interactive { get; set; }
    public LogLevel LogLevel { get; set; }
}

internal enum Verbosity { Quiet, Normal, Detailed }
