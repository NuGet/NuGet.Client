using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;

namespace NuGet.Packaging.Rules
{
    class DependeciesGroupsForEachTFMRule : IPackageRule
    {
        public string MessageFormat { get; }

        public DependeciesGroupsForEachTFMRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }
        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var files = builder.GetFiles().ToList().
                Select(t => PathUtility.GetPathWithDirectorySeparator(t));
            var packageNuspec = builder.GetNuspec();
            return Validate(files, packageNuspec);
        }

        internal IEnumerable<PackagingLogMessage> Validate(IEnumerable<string> files, Stream packageNuspec)
        {
            var tfmsFromFiles = files.
                Where(t => t.StartsWith(PackagingConstants.Folders.Lib + Path.DirectorySeparatorChar)
                    || t.StartsWith(PackagingConstants.Folders.Ref + Path.DirectorySeparatorChar)).
                Select(f => NuGetFramework.Parse(f.Split(Path.DirectorySeparatorChar)[1]).GetFrameworkString()).ToList();

            if (tfmsFromFiles != null)
            {
                List<string> tfmsFromNuspec = null;
                using (var stream = new StreamReader(packageNuspec))
                {
                    var nuspec = XDocument.Load(stream);
                    if (nuspec != null)
                    {
                        string name = nuspec.Root.Name.Namespace.ToString();
                        tfmsFromNuspec = nuspec.Descendants(XName.Get("dependencies", name)).Elements().
                            Attributes("targetFramework").Select(m => m.Value).ToList();
                    }
                }

                if (tfmsFromNuspec != null)
                {
                    if (!tfmsFromNuspec.All(tfmsFromFiles.Contains))
                    {
                        var issue = new List<PackagingLogMessage>();
                        issue.Add(PackagingLogMessage.CreateWarning(string.Format(MessageFormat),
                            NuGetLogCode.NU5128));
                        return issue;
                    }
                }

            }
            return Array.Empty<PackagingLogMessage>();
        }
    }
}
