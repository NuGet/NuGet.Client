// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents a signature check result and any additional information
    /// needed to display to the user.
    /// </summary>
    public class SignatureTrustResult
    {
        /// <summary>
        /// Trust result
        /// </summary>
        public SignatureTrust Trust { get; }

        /// <summary>
        /// SignatureTrustResult
        /// </summary>
        public SignatureTrustResult(SignatureTrust trust)
        {
            Trust = trust;
        }
    }
}
