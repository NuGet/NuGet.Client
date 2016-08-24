using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    internal class Util
    {
        private static string DotnetCliBinary { get; set; }

        private static string DotnetCliExe { get; set; }

        private static string XplatDll { get; set; }

        private static string XplatDllShell { get; set; }

        static Util()
        {
            DotnetCliBinary = @"dotnet";
            DotnetCliExe = @"dotnet.exe";
            XplatDll = @"NuGet.Core\NuGet.CommandLine.XPlat\bin\release\netcoreapp1.0\NuGet.CommandLine.XPlat.dll";
            XplatDllShell = @"NuGet.Core/NuGet.CommandLine.XPlat/bin/release/netcoreapp1.0/NuGet.CommandLine.XPlat.dll";
        }

        /// <summary>
        /// Provides the path to dotnet cli on the test machine.
        /// It traverses in the directory tree going one step up at a time and looks for cli folder.
        /// </summary>
        /// <returns>
        /// <code>String</code> containing the path to the dotnet cli within the local repository.
        /// Can return <code>null</code> if no cli directory or dotnet cli is found, in which case the tests can fail.
        /// </returns>
        public static string GetDotnetCli()
        {
            var currentDirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            var parentDirInfo = currentDirInfo.Parent;
            while (parentDirInfo != null)
            {
                var dotnetCli = "";
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
            return null;
        }

        /// <summary>
        /// Adds a few dummy text files at the specified path for testing nuget locals --clear
        /// </summary>
        /// <param name="path">Path which needs to be populated with dummy files</param>
        public static void createTestFiles(string path)
        {
            var fileNames = new List<string> { "file1.txt", "file2.txt" };
            foreach (var fileName in fileNames)
            {
                File.Create(Path.Combine(path, fileName)).Dispose();
            }
        }

        /// <summary>
        /// Provides the path to Xplat dll on the test machine.
        /// It traverses in the directory tree going one step up at a time and looks for src folder.
        /// Once in src, it looks for the xplat dll in the location specified by <code>XplatDll</code> or <code>XplatDllShell</code>.
        /// </summary>
        /// <returns>
        /// <code>String</code> containing the path to the dotnet cli within the local repository.
        /// Can return <code>null</code> if no src directory or xplat dll is found, in which case the tests can fail.
        /// </returns>
        public static string GetXplatDll()
        {
            var currentDirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            var parentDirInfo = currentDirInfo.Parent;
            while (parentDirInfo != null)
            {
                var xplatDll = "";
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
            return null;
        }

        /// <summary>
        /// Used to verify the success of positive test cases
        /// </summary>
        /// <param name="result">The actual result of the test</param>
        /// <param name="expectedOutputMessage"> The expected result of the test</param>
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

        /// <summary>
        /// Used to verify the failure of negitive test cases
        /// </summary>
        /// <param name="result">The actual result of the test</param>
        /// <param name="expectedOutputMessage"> The expected result of the test</param>
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