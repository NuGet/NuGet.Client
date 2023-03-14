// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Commands
{
    public static class ConfigPathsRunner
    {
        public static void Run(ConfigPathsArgs args, Func<ILogger> getLogger)
        {
            var settings = RunnerHelper.GetSettingsFromDirectory(args.WorkingDirectory);

            if (settings != null)
            {
                var filePaths = settings.GetConfigFilePaths();
                foreach (var filePath in filePaths)
                {
                    getLogger().LogMinimal(filePath);
                }
            }
            else
            {
                throw new CommandException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PathNotFound, args.WorkingDirectory));
            }
        }
    }
}
