using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet
{
    internal static class DebugHelper
    {
        [Conditional("DEBUG")]
        internal static void WaitForAttach(ref string[] args)
        {
            if (args.Length > 0 && (String.Equals(args[0], "dbg", StringComparison.OrdinalIgnoreCase) || String.Equals(args[0], "debug", StringComparison.OrdinalIgnoreCase)))
            {
                args = args.Skip(1).ToArray();
                if (!Debugger.IsAttached)
                {
                    Debugger.Launch();
                }
            }
        }
    }
}
