// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;

namespace NuGet.CommandLine.XPlat
{
    public class ListPackageArgs
    {
        public ILogger Logger { get; }
        public string Framework { get; set; }
        public string Outdated { get; set; }
        public string Deprecated { get; set; }
        public string Transitive { get; set; }
        
        public ListPackageArgs(ILogger logger, string framework, bool outdated, bool deprecated, bool transitive)
        {
            Logger = logger;
        }

        private void ValidateArgument(object arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }
        }
    }
}