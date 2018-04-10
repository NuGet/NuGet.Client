// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Indicates the type of signature a verification is targeting
    /// </summary>
    /// <remarks>This target makes no assumption about the placement of the signature.
    /// It only refers to author or repository type of signature.
    /// If a specific placement is needed use the SignaturePlacement class.</remarks>
    [Flags]
    public enum VerificationTarget
    {
        /// <summary>
        /// Target Author signatures
        /// </summary>
        Author      = 0x01,

        /// <summary>
        /// Target Repository signatures
        /// </summary>
        Repository  = 0x10
    }
}
