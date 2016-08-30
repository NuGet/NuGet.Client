using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    internal class DotnetCliUtil
    {
        private const string _dotnetCliBinary = @"dotnet";
        private const string _dotnetCliExe = @"dotnet.exe";

        private static string _xplatDll = Path.Combine("NuGet.Core", "NuGet.CommandLine.XPlat", "bin",
                                                       "release", "netcoreapp1.0", "NuGet.CommandLine.XPlat.dll");

        private static List<string> _fileNames = new List<string> { "file1.txt", "file2.txt" };

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
            var funcTestLocation = typeof(XPlatLocalsTests).GetTypeInfo().Assembly.Location;
            var currentDirInfo = new DirectoryInfo(Path.GetDirectoryName(funcTestLocation));
            var parentDirInfo = currentDirInfo.Parent;
            while (parentDirInfo != null)
            {
                foreach (var dir in parentDirInfo.EnumerateDirectories())
                {
                    if (StringComparer.Ordinal.Equals(dir.Name, "cli"))
                    {
                        var dotnetCli = "";
                        dotnetCli = Path.Combine(dir.FullName, _dotnetCliExe);
                        if (File.Exists(dotnetCli))
                        {
                            return dotnetCli;
                        }
                        dotnetCli = Path.Combine(dir.FullName, _dotnetCliBinary);
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
        public static void CreateTestFiles(string path)
        {
            foreach (var fileName in _fileNames)
            {
                File.Create(Path.Combine(path, fileName)).Dispose();
            }
        }

        /// <summary>
        /// Verifies the dummy text files at the specified path for testing nuget locals --clear
        /// </summary>
        /// <param name="path">Path which needs to be tested for the dummy files</param>
        public static void VerifyClearSuccess(string path)
        {
            Assert.False(Directory.Exists(path));
        }

        /// <summary>
        /// Verifies the dummy text files at the specified path for testing nuget locals --clear
        /// </summary>
        /// <param name="path">Path which needs to be tested for the dummy files</param>
        public static void VerifyNoClear(string path)
        {
            Assert.True(Directory.Exists(path));
            var files = Directory.GetFiles(path)
                                 .Select(filepath => Path.GetFileName(filepath))
                                 .ToArray();
            foreach (var filename in _fileNames)
            {
                Assert.True(Array.Exists(files, element => element == filename));
            }

            Assert.Equal(files.Count(), _fileNames.Count);
        }

        /// <summary>
        /// Provides the path to Xplat dll on the test machine.
        /// It traverses in the directory tree going one step up at a time and looks for src folder.
        /// Once in src, it looks for the xplat dll in the location specified by <code>_xplatDll</code>.
        /// </summary>
        /// <returns>
        /// <code>String</code> containing the path to the dotnet cli within the local repository.
        /// Can return <code>null</code> if no src directory or xplat dll is found, in which case the tests can fail.
        /// </returns>
        public static string GetXplatDll()
        {
            var funcTestLocation = typeof(XPlatLocalsTests).GetTypeInfo().Assembly.Location;
            var currentDirInfo = new DirectoryInfo(Path.GetDirectoryName(funcTestLocation));
            var parentDirInfo = currentDirInfo.Parent;
            while (parentDirInfo != null)
            {
                foreach (var dir in parentDirInfo.EnumerateDirectories())
                {
                    if (StringComparer.Ordinal.Equals(dir.Name, "src"))
                    {
                        var xplatDll = Path.Combine(dir.FullName, _xplatDll);
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
                "nuget DID NOT SUCCEED: Ouput is " + result.Item2 + ". Error is " + result.Item3);

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
                "nuget DID NOT FAIL: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            Assert.True(
                result.Item2.Contains(expectedErrorMessage),
                "Expected error is " + expectedErrorMessage + ". Actual error is " + result.Item2);
        }
    }
}