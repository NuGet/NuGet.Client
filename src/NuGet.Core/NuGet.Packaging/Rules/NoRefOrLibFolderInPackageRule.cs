using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

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
            var files = package.GetFiles().ToList();
            return Validate(files);
        }

        internal IEnumerable<PackagingLogMessage> Validate(IEnumerable<string> files)
        {
            var managedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
            var collection = new ContentItemCollection();
            collection.Load(files);

            var libItems = GetContentForPattern(collection, managedCodeConventions.Patterns.CompileLibAssemblies);
            var refItems = GetContentForPattern(collection, managedCodeConventions.Patterns.CompileRefAssemblies);
            var buildItems = GetContentForPattern(collection, managedCodeConventions.Patterns.MSBuildFiles);

            var libFrameworks = GetGroupFrameworks(libItems).ToArray();
            var refFrameworks = GetGroupFrameworks(refItems).ToArray();
            var buildFrameworks = GetGroupFrameworks(buildItems).ToArray();

            if (libFrameworks.Length == 0 && refFrameworks.Length == 0)
            {
                //if you can't find the ref and lib folder, then find the build folder
                if (buildFrameworks.Count() != 0)
                {
                    //if you can find any folders other than native or any, raise an NU5127
                    if (buildFrameworks.
                        Any(t => (t != "dotnet" && t != "native")))
                    {
                        var issue = new List<PackagingLogMessage>();
                        issue.Add(PackagingLogMessage.CreateWarning(string.Format(MessageFormat),
                            NuGetLogCode.NU5127));
                        return issue;
                    }
                }
            }

            return Array.Empty<PackagingLogMessage>();
        }

        private static IEnumerable<ContentItemGroup> GetContentForPattern(ContentItemCollection collection, PatternSet pattern)
        {
            return collection.FindItemGroups(pattern);
        }

        private static IEnumerable<string> GetGroupFrameworks(IEnumerable<ContentItemGroup> groups)
        {
            return groups.Select(e => ((NuGetFramework)e.Properties["tfm"]).GetShortFolderName());
        }
    }
}
