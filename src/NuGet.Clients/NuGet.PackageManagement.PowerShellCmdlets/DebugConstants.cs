// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if DEBUG
using System.IO;
namespace NuGetConsole.Host.PowerShell {
    public static class DebugConstants {
        internal static string TestModulePath = Path.Combine(@"C:\Enlist\NuGet\NuGet.Client\src\NuGet.Clients\VsConsole\PowerShellHost\..\..\..", @"test\EndToEnd\NuGet.Tests.psm1");
    }
}
#endif
