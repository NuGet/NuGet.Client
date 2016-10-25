using System.Diagnostics;

namespace NuGet.Test.Utility
{
    public class CommandRunnerResult
    {
        private readonly string _output;
        private readonly string _error;

        internal CommandRunnerResult(Process process, int exitCode, string output, string error)
        {
            Process = process;
            Item1 = exitCode;
            _output = output;
            _error = error;
        }

        public Process Process { get; }
        public int Item1 { get; }
        public string Item2 => _output;
        public string Item3 => _error;
    }
}