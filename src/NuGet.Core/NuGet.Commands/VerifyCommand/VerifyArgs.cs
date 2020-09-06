// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Common;

namespace NuGet.Commands
{
    public class VerifyArgs
    {
        /// <summary>
        /// Available types of verification.
        /// </summary>
        public enum Verification
        {
            Unknown,
            All,
            Signatures
        };

        /// <summary>
        /// Types of verifications to be performed.
        /// </summary>
        public IList<Verification> Verifications { get; set; }

#if NETFRAMEWORK
        /// <summary>
        /// Path to the package that has to be verified.
        /// </summary>
        public string PackagePath { get; set; }
#else
        /// <summary>
        /// Paths to the packages that has to be verified.
        /// </summary>
        public IReadOnlyList<string> PackagesPath { get; set; }
#endif

        /// <summary>
        /// Logger to be used to display the logs during the execution of sign command.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Amount of detail the logger should receive
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// If not empty, signer certificate fingerprint must match one in this list
        /// </summary>
        public IEnumerable<string> CertificateFingerprint { get; set; }
    }
}
