using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("NuGet.Console")]
[assembly: AssemblyDescription("Package Manager Powershell Console")]

[assembly: InternalsVisibleTo("NuGet.PowerShellHost.Test")]

// dynamic assembly used by Moq to host proxies
#pragma warning disable 1700
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#pragma warning restore 1700

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("3f7df594-3ac4-4ed6-93c3-1dcfee7600c6")]
