// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;

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

        /// <summary>
        /// Path to the package that has to be signed.
        /// </summary>
        public string PackagePath { get; set; }

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

        /// <summary>
        /// Settings read from the config file provided by the user. Defaults to settings from %AppData%\NuGet\NuGet.config
        /// </summary>
        public ISettings Settings { get; set; }
    }
}
