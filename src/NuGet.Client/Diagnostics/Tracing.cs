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
