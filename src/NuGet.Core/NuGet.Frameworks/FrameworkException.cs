﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Frameworks
{
#if NUGET_FRAMEWORKS_INTERNAL
    internal
#else
    public
#endif
    class FrameworkException : Exception
    {
        public FrameworkException(string message)
            : base(message)
        {
        }
    }
}
