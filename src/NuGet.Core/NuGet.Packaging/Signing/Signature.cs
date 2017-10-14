// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Package signature information.
    /// </summary>
    public class Signature
    {
        /// <summary>
        /// Indicates if this is an author or repository signature.
        /// </summary>
        public SignatureType Type { get; set; }

        /// <summary>
        /// Signature friendly name.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Certificate used to generate the signature.
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// The hash algorithm used to generate TargetHashValue.
        /// </summary>
        public HashAlgorithmName TargetHashAlgorithm { get; set; }

        /// <summary>
        /// The Base64-encoded hash of the byte stream of the manifest file. 
        /// </summary>
        public string TargetHashValue { get; set; }

        /// <summary>
        /// Additional counter signatures.
        /// </summary>
        public IReadOnlyList<Signature> AdditionalSignatures { get; set; } = new List<Signature>();

        /// <summary>
        /// TEMPORARY - trust result to return.
        /// </summary>
        public SignatureVerificationStatus TestTrust { get; set; }
    }
}
