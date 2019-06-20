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
        private static readonly string BuildDir = PackagingConstants.Folders.Build + Path.DirectorySeparatorChar;
        public string MessageFormat { get; }

        public NoRefOrLibFolderInPackageRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader package)
        {
            var files = package.GetFiles().ToList().Select(t => PathUtility.GetPathWithDirectorySeparator(t));
            if (!files.
                Any(t => t.StartsWith(PackagingConstants.Folders.Lib + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || t.StartsWith(PackagingConstants.Folders.Ref + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                //if you can't find the ref and lib folder, then find the build folder
                if (files.
                    Select(t => PathUtility.GetPathWithDirectorySeparator(t)).
                    Any(t => t.StartsWith(BuildDir, StringComparison.OrdinalIgnoreCase)))
                {
                    //if you can find any folders other than native or any, raise an NU5127
                    if (files.
                        Where(t => t.StartsWith(BuildDir)).
                        Any(t => !t.StartsWith(BuildDir + PackagingConstants.Folders.Native + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase)
                            && !t.StartsWith(BuildDir + PackagingConstants.Folders.Any + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        var issue = new List<PackagingLogMessage>();
                        issue.Add(PackagingLogMessage.CreateWarning(string.Format(MessageFormat,package.NuspecReader.GetId()),
                            NuGetLogCode.NU5127));
                        return issue;
                    }
                }
            }

            return Array.Empty<PackagingLogMessage>();
        }
    }
}
