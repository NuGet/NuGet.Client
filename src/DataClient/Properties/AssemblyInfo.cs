using System;
using System.Reflection;
using System.Resources;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
// Project-specific attributes
[assembly: AssemblyTitle("NuGet.DataClient")]
[assembly: AssemblyDescription("A NuGet configuration-aware HTTP client with caching support")]

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
[assembly: AssemblyInformationalVersion("3.0.0-rc")]
#endif
