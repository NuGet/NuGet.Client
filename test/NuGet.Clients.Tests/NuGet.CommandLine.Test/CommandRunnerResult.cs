namespace NuGet.CommandLine.Test
{
    public class CommandRunnerResult
    {
        public CommandRunnerResult(int exitCode, string output, string error)
        {
            Item1 = exitCode;
            Item2 = output;
            Item3 = error;
        }

        public int Item1 { get; }
        public string Item2 { get; }
        public string Item3 { get; }
    }
}
