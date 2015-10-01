#if DEBUG
using System.IO;
namespace NuGetConsole.Host.PowerShell {
    public static class DebugConstants {
        internal static string TestModulePath = Path.Combine(@"D:\dev\NuGet.Client\src\NuGet.Clients\VsConsole\PowerShellHost\..\..\..", @"test\EndToEnd\NuGet.Tests.psm1");
    }
}
#endif
