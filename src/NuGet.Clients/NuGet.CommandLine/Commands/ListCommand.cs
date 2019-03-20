using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.CommandLine.Commands
{
    [DeprecatedCommand(typeof(SearchCommand))]
    [Command(
         typeof(NuGetCommand),
         "list",
        "ListCommandDescription",
        UsageSummaryResourceName = "ListCommandUsageSummary",
        UsageDescriptionResourceName = "ListCommandUsageDescription",
        UsageExampleResourceName = "ListCommandUsageExamples")]
    public class ListCommand : SearchCommand
    {
    }
}
