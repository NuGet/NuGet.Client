// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Timeout utilities.
    /// </summary>
    public static class TimeoutUtilities
    {
        /// <summary>
        /// Determines if a timeout is valid.
        /// </summary>
        /// <param name="timeout">A timeout.</param>
        /// <returns><c>true</c> if the timeout is valid; otherwise, <c>false</c>.</returns>
        public static bool IsValid(TimeSpan timeout)
        {
            if (ProtocolConstants.MinTimeout <= timeout && timeout <= ProtocolConstants.MaxTimeout)
            {
                return true;
            }

            return false;
        }
    }
}