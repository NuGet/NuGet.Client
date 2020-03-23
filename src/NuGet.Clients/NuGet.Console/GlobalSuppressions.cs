// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Microsoft.Design", "CA1017:MarkAssembliesWithComVisible")]
[assembly: SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames")]
[assembly: SuppressMessage("Microsoft.Design", "CA1014:MarkAssembliesWithClsCompliant")]
[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "NuGetConsole.Implementation")]
[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "NuGetConsole.Implementation.Console")]
[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "NuGetConsole.Implementation.PowerConsole")]
[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "NuGetConsole.DebugConsole")]
[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "NuGetConsole.Host.PowerShell")]
[assembly: SuppressMessage("Usage", "VSTHRD110:Observe result of async calls", Justification = "https://github.com/NuGet/Home/issues/7674", Scope = "member", Target = "~M:NuGetConsole.Implementation.Console.ConsoleDispatcher.Start")]
[assembly: SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Dispose method", Scope = "member", Target = "~M:NuGetConsole.ChannelOutputConsole.Dispose(System.Boolean)")]
