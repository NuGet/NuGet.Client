using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    public class IconMaxFilesizeRule : IPackageRule
    {
        public string MessageFormat { get; }

        public IconMaxFilesizeRule(string messageFormat)
        {
            MessageFormat = messageFormat ?? throw new ArgumentNullException(nameof(messageFormat));
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
        }
    }
}
