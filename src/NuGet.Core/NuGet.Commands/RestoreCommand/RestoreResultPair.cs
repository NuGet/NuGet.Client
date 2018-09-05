using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Commands
{
    public class RestoreResultPair
    {
        public RestoreSummaryRequest SummaryRequest { get; }

        public RestoreResult Result { get; }

        public RestoreResultPair(RestoreSummaryRequest request, RestoreResult result)
        {
            SummaryRequest = request;
            Result = result;
        }
    }
}
