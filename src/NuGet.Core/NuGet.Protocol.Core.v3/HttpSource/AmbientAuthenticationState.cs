﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Represents source authentication status per active operation
    /// </summary>
    public class AmbientAuthenticationState
    {
        public bool IsBlocked { get; set; }
        public int AuthenticationRetriesCount { get; set; }

        public void Increment()
        {
            AuthenticationRetriesCount++;

            if (AuthenticationRetriesCount > HttpHandlerResourceV3Provider.MaxAuthRetries)
            {
                IsBlocked = true;
            }
        }
    }
}
