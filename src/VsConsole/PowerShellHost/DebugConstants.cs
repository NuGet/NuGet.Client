#if DEBUG
using System.IO;
namespace NuGetConsole.Host.PowerShell {
    public static class DebugConstants {
        internal static string TestModulePath = Path.Combine(@"F:\dd\git\NuGet.VisualStudioExtension\src\VsConsole\PowerShellHost\..\..\..", @"test\EndToEnd\NuGet.Tests.psm1");
    }
}
#endif
