using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly: AssemblyCompany(".NET Foundation")]
[assembly: AssemblyProduct("NuGet")]
[assembly: AssemblyCopyright("\x00a9 .NET Foundation. All rights reserved.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyTrademark("")]

[assembly: ComVisible(false)]

// When built on the build server, the NuGet release version is specified in
// Build\Build.proj.
// When built locally, the NuGet release version is the values specified in this file.

// Note that FIXED_ASSEMBLY_VERSION is defined in project VisualStudio.Interop so that
// the version defined here is not used for VisualStudio.Interop.Dll. Instead, the version
// of that DLL is always "1.0.0.0".
#if !FIXED_ASSEMBLY_VERSION
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyInformationalVersion("3.0.0-beta")]
#endif

[assembly: NeutralResourcesLanguage("en-US")]
