// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace NuGet.Client.Diagnostics
{
    internal static class Tracing
    {
        private static long _nextInvocationId = 0;

        public static long GetNextInvocationId()
        {
            return Interlocked.Increment(ref _nextInvocationId);
        }
    }
}
