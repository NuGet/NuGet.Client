// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Message methods.
    /// </summary>
    public enum MessageMethod
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// Get operation claims
        /// </summary>
        GetOperationClaims,

        /// <summary>
        /// Handshake
        /// </summary>
        Handshake,

        /// <summary>
        /// Initialize
        /// </summary>
        Initialize,

        /// <summary>
        /// Log
        /// </summary>
        Log
    }
}