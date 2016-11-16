// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


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
