using System.ComponentModel.Composition;
using NuGet;
using NuGet.CommandLine;

namespace SampleCommandLineExtensions
{
    [Export]
    [Command("hello", "Says \"Hello!\"",
        UsageExample = "nuget example", UsageSummary = "This Command says hello")]
    public class HelloCommand : Command
    {
        public override void ExecuteCommand()
        {
            Console.WriteLine("Hello!");
        }
    }
}
