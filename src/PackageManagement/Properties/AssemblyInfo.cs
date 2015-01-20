using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

// Project-specific attributes
[assembly: AssemblyTitle("NuGet's core package management features")]
[assembly: AssemblyDescription("NuGet's core package management features")]

// Common attributes
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("NuGet")]
[assembly: AssemblyCopyright("Copyright 2015 Microsoft. NuGet is made possible by the Microsoft Corporation's open source project.")]

[assembly: NeutralResourcesLanguage("en-US")]
[assembly: CLSCompliant(true)]

// When built on the build server, the NuGet release version is specified by the build.
// When built locally, the NuGet release version is the values specified in this file.
#if !FIXED_ASSEMBLY_VERSION
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyInformationalVersion("3.0.0-rc")]
#endif
