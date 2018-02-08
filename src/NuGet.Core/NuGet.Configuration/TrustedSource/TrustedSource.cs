// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    public class TrustedSource
    {
        /// <summary>
        /// Name of the associated package source.
        /// </summary>
        public string SourceName { get; }

        /// <summary>
        /// Service index of the source.
        /// </summary>
        public string ServiceIndex { get; set; }

        /// <summary>
        /// List of trusted certificates.
        /// </summary>
        public ISet<CertificateTrustEntry> Certificates { get; }

        public TrustedSource(string source)
        {
            SourceName = source ?? throw new ArgumentNullException(nameof(source));
            Certificates = new HashSet<CertificateTrustEntry>();
        }
    }
}
