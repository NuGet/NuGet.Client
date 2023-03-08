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

        // Add in ignore inaccessible directory arg functionality
        {
            var settings = RunnerHelper.GetSettings(args.WorkingDirectory);
            //var filePaths = settings.GetConfigFilePaths();

            //foreach (var filePath in filePaths)
            //{
            //    getLogger().LogMinimal(filePath);
            //}

            if (settings != null)
            {
                var filePaths = settings.GetConfigFilePaths();
                foreach (var filePath in filePaths)
                {
                    getLogger().LogMinimal(filePath);
                }
            }
            else
            {   // Raise error for non-existing directory arg
                //getLogger().LogError(string.Format(CultureInfo.CurrentCulture, Strings.Error_PathNotFound, args.WorkingDirectory));
                // Using placeholder error msg for testing; Error_PathNotFound still not showing up after rebuilding project
                getLogger().LogError("DIRECTORY PATH NOT FOUND");
            }

        }

    }
}
