// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Plugin protocol constants.
    /// </summary>
    public static class ProtocolConstants
    {
        /// <summary>
        /// The current protocol version.
        /// </summary>
        public static readonly SemanticVersion CurrentVersion = new SemanticVersion(major: 1, minor: 0, patch: 0);

        /// <summary>
        /// The maximum timeout value.
        /// </summary>
        /// <remarks>This is set by CancellationTokenSource's constructor.</remarks>
        public static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

        /// <summary>
        /// The minimum timeout value.
        /// </summary>
        public static readonly TimeSpan MinTimeout = TimeSpan.FromTicks(1);
    }
}