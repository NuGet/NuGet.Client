using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

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
            var files = builder.GetFiles();
            var packageNuspec = builder.GetNuspec();
            return Validate(files, packageNuspec);
        }

        internal IEnumerable<PackagingLogMessage> Validate(IEnumerable<string> files, Stream packageNuspec)
        {
            var managedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
            var compareTFMs = managedCodeConventions.Properties["tfm"].CompatibilityTest;
            var collection = new ContentItemCollection();
            collection.Load(files);


            var libItems = GetContentForPattern(collection, managedCodeConventions.Patterns.CompileLibAssemblies);
            var refItems = GetContentForPattern(collection, managedCodeConventions.Patterns.CompileRefAssemblies);
            var libFrameworks = GetGroupFrameworks(libItems).ToArray();
            var refFrameworks = GetGroupFrameworks(refItems).ToArray();

            var tfmsFromFilesSet = new HashSet<NuGetFramework>();
            tfmsFromFilesSet.AddRange(libFrameworks);
            tfmsFromFilesSet.AddRange(refFrameworks);
            var tfmsFromFiles = tfmsFromFilesSet.ToList();

            if (tfmsFromFiles.Count != 0)
            {
                List<NuGetFramework> tfmsFromNuspec = null;
                using (var stream = new StreamReader(packageNuspec))
                {
                    var nuspec = XDocument.Load(stream);
                    if (nuspec != null)
                    {
                        string name = nuspec.Root.Name.Namespace.ToString();
                        tfmsFromNuspec = nuspec.Descendants(XName.Get("dependencies", name)).Elements().
                            Attributes("targetFramework").Select(f => NuGetFramework.Parse(f.Value)).ToList();
                    }
                }

                if (tfmsFromNuspec != null)
                {
                    
                    var issue = new List<PackagingLogMessage>();
                    foreach(var item in tfmsFromFiles)
                    {
                        foreach(var nuspecTFMs in tfmsFromNuspec)
                        {
                            var meh = compareTFMs(item, nuspecTFMs) && !tfmsFromNuspec.Contains(item);
                        }
                        //var meh = managedCodeConventions.Properties["tfm"].CompatibilityTest(tfm;
                        if (!tfmsFromNuspec.Contains(item))
                        {
                            issue.Add(PackagingLogMessage.CreateWarning(string.Format(MessageFormat),
                                NuGetLogCode.NU5128));
                        }
                    }
                    if(issue.Count != 0)
                    {
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

        private static IEnumerable<NuGetFramework> GetGroupFrameworks(IEnumerable<ContentItemGroup> groups)
        {

            return groups.Select(e => (NuGetFramework)e.Properties["tfm"]);
        }

        
    }
}
