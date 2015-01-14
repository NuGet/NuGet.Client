using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("PowerConsolePowerShellHost")]
[assembly: AssemblyDescription("PowerConsole PowerShell host implementation")]

[assembly: InternalsVisibleTo("NuGet.PowerShellHost.Test")]

// dynamic assembly used by Moq to host proxies
#pragma warning disable 1700
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#pragma warning restore 1700

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("d2b73aac-236b-4676-9522-c487b838c1de")]
