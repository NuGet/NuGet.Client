// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Commands
{
    public static class ConfigPathsRunner
    {
        public static void Run(ConfigPathsArgs args, Func<ILogger> getLogger)
        {
            // Do I need to explicitly add something to handle null arg values? code already seems to work w/ null value
            var settings = RunnerHelper.GetSettings(args.WorkingDirectory);
            var filePaths = settings.GetConfigFilePaths();

            foreach (var filePath in filePaths)
            {
                getLogger().LogMinimal(filePath);
            }
        }

    }
}
