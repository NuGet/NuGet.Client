// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Configuration;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Core.Types
{
    public class PackageProgressEventArgs : EventArgs
    {
        private readonly PackageIdentity _identity;
        private readonly PackageSource _source;
        private readonly string _operation;
        private readonly double _progressPercentage;

        /// <summary>
        /// The status of a package action.
        /// </summary>
        /// <param name="identity">package identity</param>
        /// <param name="source">repository source or null</param>
        /// <param name="operation">The package action.</param>
        /// <param name="progressPercentage">0.0 - 1.0</param>
        public PackageProgressEventArgs(PackageIdentity identity, PackageSource source, string operation, double progressPercentage)
        {
            if (progressPercentage < 0
                || progressPercentage > 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            _identity = identity;
            _source = source;
            _operation = operation;
            _progressPercentage = progressPercentage;
        }

        public PackageIdentity PackageIdentity
        {
            get { return _identity; }
        }

        public PackageSource PackageSource
        {
            get { return _source; }
        }

        /// <summary>
        /// Completion - 0.0 - 1.0
        /// </summary>
        public double ProgressPercentage
        {
            get { return _progressPercentage; }
        }

        /// <summary>
        /// True at 100% completion
        /// </summary>
        public bool IsComplete
        {
            get { return _progressPercentage == 1; }
        }

        public bool HasPackageSource
        {
            get { return PackageSource != null; }
        }

        public string Operation
        {
            get { return _operation; }
        }
    }
}
