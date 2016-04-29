// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    /// <summary>
    /// Represents source authentication status per active operation
    /// </summary>
    public class AmbientAuthenticationState
    {
        internal const int MaxAuthRetries = 3;

        public bool IsBlocked { get; set; } = false;
        public int AuthenticationRetriesCount { get; set; }

        public void Increment()
        {
            AuthenticationRetriesCount++;

            if (AuthenticationRetriesCount > MaxAuthRetries)
            {
                IsBlocked = true;
            }
        }
    }
}
