using System.Diagnostics;

namespace NuGet.Test.Utility
{
    public class CommandRunnerResult
    {
        public Process Process { get; }
        public int Item1 { get; }
        public string Item2 { get; }
        public string Item3 { get; }

        internal CommandRunnerResult(Process process, int exitCode, string output, string error)
        {
            Process = process;
            Item1 = exitCode;
            Item2 = output;
            Item3 = error;
        }
    }
}