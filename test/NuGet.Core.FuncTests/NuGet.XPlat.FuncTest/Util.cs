using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class Util
    {
        private static string DotnetCliExec { get; set; }

        static Util()
        {
            DotnetCliExec = @"../../../cli/dotnet";
        }

        public static string GetDotnetCli()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var dotnetCli = Path.Combine(currentDir, DotnetCliExec);
            return dotnetCli;
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