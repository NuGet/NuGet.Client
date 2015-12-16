using System.Diagnostics;

namespace NuGet.Test.Utility
{
    public class CommandRunnerResult
    {
        internal CommandRunnerResult(Process process, int exitCode, LockedStringBuilder output, LockedStringBuilder error)
        {
            Process = process;
            Item1 = exitCode;
            _output = output;
            _error = error;
        }

        private readonly LockedStringBuilder _output;
        private readonly LockedStringBuilder _error;

        public Process Process { get; }
        public int Item1 { get; }
        public string Item2 => _output.ToString();
        public string Item3 => _error.ToString();
    }
}
