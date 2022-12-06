// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Test.Utility;

namespace NuGet.CommandLine.Test.Caching
{
    public interface INuGetExe
    {
        void ClearHttpCache(CachingTestContext context);
        string GetHttpCachePath(CachingTestContext context);
        CommandRunnerResult Execute(CachingTestContext context, string args);
    }
}
