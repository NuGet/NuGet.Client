using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace NuGet.Client
{
    public class SearchFilter
    {
        public IEnumerable<string> SupportedFrameworks { get; set; }

        public bool IncludePrerelease { get; set; }

        public bool IncludeDelisted { get; set; }
    }
}
