using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("NuGet.VisualStudio")]
[assembly: AssemblyDescription("APIs for invoking NuGet services in Visual Studio.")]

//[assembly: InternalsVisibleTo("NuGet.VisualStudio.Test")]
//[assembly: InternalsVisibleTo("NuGet.Cmdlets.Test")]

// We're not really importing anything from a type library. This is just to make VS happy so we can embed interop types when 
// referencing this assembly
[assembly: ImportedFromTypeLib("NuGet.VisualStudio")]
[assembly: Guid("228F7591-2777-47D7-B81D-FEADFC71CEB5")]