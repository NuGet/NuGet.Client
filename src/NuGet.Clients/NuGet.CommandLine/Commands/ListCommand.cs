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
    [Obsolete(message:"Use SearchCommand class. This class will disappear in upcoming releases", error: false)]
    public class ListCommand : SearchCommand
    {
    }
}
