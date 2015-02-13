using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

// Project-specific attributes
[assembly: AssemblyTitle("NuGet.Client.V3.VisualStudio")]
[assembly: AssemblyDescription("NuGet's Visual Studio scenario resource client for API v3")]

// Common attributes
[assembly: AssemblyCompany("Outercurve Foundation")]
[assembly: AssemblyProduct("NuGet")]
[assembly: AssemblyCopyright("Copyright Outercurve Foundation. All rights reserved.")]

[assembly: NeutralResourcesLanguage("en-US")]
[assembly: CLSCompliant(true)]

// When built on the build server, the NuGet release version is specified by the build.
// When built locally, the NuGet release version is the values specified in this file.
#if !FIXED_ASSEMBLY_VERSION
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyInformationalVersion("3.0.0-beta")]
#endif
