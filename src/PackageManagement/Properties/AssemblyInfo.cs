// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

// Project-specific attributes

[assembly: AssemblyTitle("NuGet's core package management features")]
[assembly: AssemblyDescription("NuGet's core package management features")]

// Common attributes

[assembly: AssemblyCompany(".NET Foundation")]
[assembly: AssemblyProduct("NuGet")]
[assembly: AssemblyCopyright("Copyright .NET Foundation. All rights reserved.")]
[assembly: NeutralResourcesLanguage("en-US")]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]

// When built on the build server, the NuGet release version is specified by the build.
// When built locally, the NuGet release version is the values specified in this file.
#if !FIXED_ASSEMBLY_VERSION

[assembly: AssemblyVersion("3.1.0.0")]
[assembly: AssemblyInformationalVersion("3.1.0")]
#endif
