using System;
using System.Diagnostics;
using System.IO;

namespace NuGet.Shared
{
    public class UpdateAssemblyInfo
    {
        private const string assemblyInfoFileName = "CommonAssemblyInfo.generated.cs";

        public static void Main(string[] args)
        {
            // Write out git commit hash to a generated assembly info file
            var assemblyInfoText = GetAssemblyInfoFileText();
            var assemblyInfoFullFileName = Path.GetFullPath(Path.Combine("..", assemblyInfoFileName));

            // We don't want to re-write the file if the commit hash has not change (since that will trigger all projects being dirty).
            var existingText = File.Exists(assemblyInfoFullFileName) ? File.ReadAllText(assemblyInfoFullFileName) : "";
            if (existingText != assemblyInfoText)
            {
                Console.WriteLine($"Writing new content for '{assemblyInfoFileName}':\n{assemblyInfoText}");
                File.WriteAllText(assemblyInfoFullFileName, assemblyInfoText);
            }
            else
            {
                Console.WriteLine($"Content of '{assemblyInfoFileName}' has not changed.");
            }
        }

        private static string GetAssemblyInfoFileText()
        {
            var commitHash = GetGitCommitHash();
            return $"#if !IS_NET40_CLIENT\n[assembly: System.Reflection.AssemblyMetadata(\"CommitHash\", \"{commitHash}\")]\n#endif";
        }

        private static string GetGitCommitHash()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("git.exe") { RedirectStandardOutput = true, Arguments = "rev-parse HEAD" };
            var process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();
            return process.StandardOutput.ReadToEnd().Trim();
        }
    }
}
