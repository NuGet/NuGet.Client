// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal class WhyPackageArgs
    {
        public ILogger Logger { get; }
        public string Path { get; }
        public string Package { get; }
        public IEnumerable<string> Frameworks { get; }


        /// <summary>
        /// A constructor for the arguments of Why package
        /// command. This is used to execute the runner's
        /// method
        /// </summary>
        /// <param name="path"> The path to the solution or project file </param>
        /// <param name="package">The package to look up the dependency paths for </param>
        /// <param name="frameworks"> The user inputted frameworks to look up for their packages </param>
        /// <param name="logger"></param>
        public WhyPackageArgs(
            string path,
            string package,
            IEnumerable<string> frameworks,
            ILogger logger)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }
}
