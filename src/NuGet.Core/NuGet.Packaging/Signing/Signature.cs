// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        /// TEMPORARY - trust result to return.
        /// </summary>
        public SignatureTrust TestTrust { get; set; }
    }
}
