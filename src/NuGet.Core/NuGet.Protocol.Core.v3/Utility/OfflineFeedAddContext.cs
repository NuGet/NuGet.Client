// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.Core.Types
{
    public class OfflineFeedAddContext
    {
        public string PackagePath { get; }
        public string Source { get; }
        public Common.ILogger Logger { get; }
        public bool ThrowIfSourcePackageIsInvalid { get; }
        public bool ThrowIfPackageExistsAndInvalid { get; }
        public bool ThrowIfPackageExists { get; }
        public bool Expand { get; }

        public OfflineFeedAddContext(
            string packagePath,
            string source,
            Common.ILogger logger,
            bool throwIfSourcePackageIsInvalid,
            bool throwIfPackageExistsAndInvalid,
            bool throwIfPackageExists,
            bool expand)
        {
            if (string.IsNullOrEmpty(packagePath))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Argument_Cannot_Be_Null_Or_Empty,
                    nameof(packagePath)));
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Argument_Cannot_Be_Null_Or_Empty, 
                    nameof(source)));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            PackagePath = packagePath;
            Source = source;
            Logger = logger;
            ThrowIfSourcePackageIsInvalid = throwIfSourcePackageIsInvalid;
            ThrowIfPackageExists = throwIfPackageExists;
            ThrowIfPackageExistsAndInvalid = throwIfPackageExistsAndInvalid;
            Expand = expand;
        }
    }
}
