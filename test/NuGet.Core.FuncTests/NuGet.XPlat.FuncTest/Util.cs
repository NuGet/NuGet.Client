using System;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class Util
    {
        private static string DotnetCliBinary { get; set; }

        private static string DotnetCliExe { get; set; }

        private static string XplatDll { get; set; }

        private static string XplatDllShell { get; set; }

        static Util()
        {
            DotnetCliBinary = @"dotnet";
            DotnetCliExe = @"dotnet.exe";
            XplatDll = @"NuGet.Core\NuGet.CommandLine.XPlat\bin\debug\netcoreapp1.0\NuGet.CommandLine.XPlat.dll";
            XplatDllShell = @"NuGet.Core/NuGet.CommandLine.XPlat/bin/Debug/netcoreapp1.0/NuGet.CommandLine.XPlat.dll";
        }

        public static string GetDotnetCli()
        {
            var currentDirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            var parentDirInfo = currentDirInfo.Parent;
            var dotnetCli = "";
            while (parentDirInfo != null)
            {
                foreach (var dir in parentDirInfo.EnumerateDirectories())
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(dir.Name, "cli"))
                    {
                        dotnetCli = Path.Combine(dir.FullName, DotnetCliExe);
                        if (File.Exists(dotnetCli))
                        {
                            return dotnetCli;
                        }
                        dotnetCli = Path.Combine(dir.FullName, DotnetCliBinary);
                        if (File.Exists(dotnetCli))
                        {
                            return dotnetCli;
                        }
                    }
                }
                currentDirInfo = new DirectoryInfo(parentDirInfo.FullName);
                parentDirInfo = currentDirInfo.Parent;
            }
            return dotnetCli;
        }

        public static string GetXplatDll()
        {
            var currentDirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            var parentDirInfo = currentDirInfo.Parent;
            var xplatDll = "";
            while (parentDirInfo != null)
            {
                foreach (var dir in parentDirInfo.EnumerateDirectories())
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(dir.Name, "src"))
                    {
                        xplatDll = Path.Combine(dir.FullName, XplatDll);
                        if (File.Exists(xplatDll))
                        {
                            return xplatDll;
                        }
                        xplatDll = Path.Combine(dir.FullName, XplatDllShell);
                        if (File.Exists(xplatDll))
                        {
                            return xplatDll;
                        }
                    }
                }
                currentDirInfo = new DirectoryInfo(parentDirInfo.FullName);
                parentDirInfo = currentDirInfo.Parent;
            }
            return xplatDll;
        }

        public static void VerifyResultSuccess(CommandRunnerResult result, string expectedOutputMessage = null)
        {
            Assert.True(
                result.Item1 == 0,
                "nuget.exe DID NOT SUCCEED: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            if (!string.IsNullOrEmpty(expectedOutputMessage))
            {
                Assert.Contains(
                    expectedOutputMessage,
                    result.Item2);
            }
        }

        public static void VerifyResultFailure(CommandRunnerResult result,
                                               string expectedErrorMessage)
        {
            Assert.True(
                result.Item1 != 0,
                "nuget.exe DID NOT FAIL: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            Assert.True(
                result.Item2.Contains(expectedErrorMessage),
                "Expected error is " + expectedErrorMessage + ". Actual error is " + result.Item2);
        }
    }
}