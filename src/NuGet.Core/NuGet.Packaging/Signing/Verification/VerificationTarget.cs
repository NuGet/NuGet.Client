// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Indicates the signature type targeted for verification.
    /// </summary>
    /// <remarks>This flag does not make any asumptions on the placement of the signature</remarks>
    [Flags]
    public enum VerificationTarget
    {
        /// <summary>
        /// The verified signature has to be an author signature
        /// </summary>
        Author          = 0x000001,

        /// <summary>
        /// The verified signature has to be a repository signature
        /// </summary>
        Repository      = 0x000010
    }
}