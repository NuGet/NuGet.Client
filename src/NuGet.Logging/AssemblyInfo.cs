// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

// Project-specific attributes

[assembly: AssemblyTitle("NuGet.Logging.")]
[assembly: AssemblyDescription("NuGet's logging contracts.")]

// Common attributes

[assembly: AssemblyCompany(".NET Foundation")]
[assembly: AssemblyProduct("NuGet")]
[assembly: AssemblyCopyright("Copyright .NET Foundation. All rights reserved.")]
[assembly: CLSCompliant(true)]

// When built on the build server, the NuGet release version is specified by the build.
// When built locally, the NuGet release version is the values specified in this file.
#if !FIXED_ASSEMBLY_VERSION

[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyInformationalVersion("3.0.0-beta")]
#endif
