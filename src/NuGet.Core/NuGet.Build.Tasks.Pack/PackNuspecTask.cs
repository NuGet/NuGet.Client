using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Build.Tasks.Pack
{
    public class PackNuspecTask : Microsoft.Build.Utilities.Task
    {
        public string NuspecFile { get; set; }
        public override bool Execute()
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            return true;
        }
    }
}
