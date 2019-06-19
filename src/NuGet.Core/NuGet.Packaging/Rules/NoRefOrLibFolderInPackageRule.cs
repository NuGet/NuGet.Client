using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    class NoRefOrLibFolderInPackageRule : IPackageRule
    {
        public string MessageFormat { get; }

        public NoRefOrLibFolderInPackageRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            if (!builder.GetFiles()
                .Select(t => PathUtility.GetPathWithDirectorySeparator(t))
                .Any(t => t.StartsWith(PackagingConstants.Folders.Lib + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || t.StartsWith(PackagingConstants.Folders.Ref + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                //if you can't find the ref and lib folder, then find the build folder
                if (builder.GetFiles()
                .Select(t => PathUtility.GetPathWithDirectorySeparator(t))
                .Any(t => t.StartsWith(PackagingConstants.Folders.Build + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                {
                    //if you can't find any folders other than native or any, raise an NU5127
                    if (!builder.GetFiles()
                    .Select(t => PathUtility.GetPathWithDirectorySeparator(t))
                    .Where(t => t.StartsWith(PackagingConstants.Folders.Build + Path.DirectorySeparatorChar))
                    .Any(t => !t.StartsWith(PackagingConstants.Folders.Build + Path.DirectorySeparatorChar + PackagingConstants.Folders.Native + Path.DirectorySeparatorChar
                    , StringComparison.OrdinalIgnoreCase)
                    && !t.StartsWith(PackagingConstants.Folders.Build + Path.DirectorySeparatorChar + PackagingConstants.Folders.Any + Path.DirectorySeparatorChar
                    , StringComparison.OrdinalIgnoreCase)))
                    {
                        var issue = new List<PackagingLogMessage>();
                        issue.Add(PackagingLogMessage.CreateWarning(string.Format(MessageFormat,builder.NuspecReader.GetId()), NuGetLogCode.NU5127));
                        return issue;
                    }
                }
            }
            
            return new List<PackagingLogMessage>();
        }
    }
}
