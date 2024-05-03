// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;

namespace NuGet.CommandLine.XPlat
{
    internal class WhyCommandArgs
    {
        public ILogger Logger { get; }
        public string Path { get; }
        public string Package { get; }
        public List<string> Frameworks { get; }

        /// <summary>
        /// A constructor for the arguments of the 'why' command.
        /// </summary>
        /// <param name="path">The path to the solution or project file.</param>
        /// <param name="package">The package for which we show the dependency graphs.</param>
        /// <param name="frameworks">The target framework(s) for which we show the dependency graphs.</param>
        /// <param name="logger"></param>
        public WhyCommandArgs(
            string path,
            string package,
            List<string> frameworks,
            ILogger logger)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ValidatePathArgument(Path);
            ValidatePackageArgument(Package);
            ValidateFrameworksOption(Frameworks);
        }

        private static void ValidatePathArgument(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                    "<PROJECT>|<SOLUTION>"));
            }

            if (!File.Exists(path)
                || (!path.EndsWith("proj", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_PathIsMissingOrInvalid,
                    path));
            }
        }

        private static void ValidatePackageArgument(string package)
        {
            if (string.IsNullOrEmpty(package))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                    "<PACKAGE_NAME>"));
            }
        }

        private static void ValidateFrameworksOption(List<string> frameworks)
        {
            var parsedFrameworks = frameworks.Select(f =>
                                    NuGetFramework.Parse(
                                        f.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim())
                                            .ToArray()[0]));

            if (parsedFrameworks.Any(f => f.Framework.Equals("Unsupported", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_InvalidFramework));
            }
        }
    }
}
