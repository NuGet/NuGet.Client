// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat.Commands.PackageDownload;

internal class PackageDownloadArgs
{
    public PackageDownloadArgs(string packageId, IList<string> sources, string outputDirectory, ILogger logger)
    {
        PackageId = packageId;
        Sources = sources;
        OutputDirectory = outputDirectory;
        Logger = logger;
    }

    public string PackageId { get; set; }
    public string Version { get; set; }
    public IList<string> Sources { get; set; }
    public string OutputDirectory { get; set; }
    public string ConfigFile { get; set; }

    public bool IncludePrerelease { get; set; }
    public bool DownloadOnly { get; set; }
    public bool AllowInsecureConnections { get; set; }
    public bool Interactive { get; set; }

    private Verbosity _verbosity = Verbosity.Normal;
    public Verbosity Verbosity
    {
        get => _verbosity;
        set => _verbosity = value;
    }

    public ILogger Logger { get; set; }

    public void SetVerbosity(string level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            _verbosity = Verbosity.Normal;
            return;
        }

        switch (level.Trim().ToLowerInvariant())
        {
            case "quiet":
                _verbosity = Verbosity.Quiet;
                break;
            case "normal":
                _verbosity = Verbosity.Normal;
                break;
            case "detailed":
                _verbosity = Verbosity.Detailed;
                break;
            default:
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidVerbosity, level));
        }
    }
}

internal enum Verbosity { Quiet, Normal, Detailed }
