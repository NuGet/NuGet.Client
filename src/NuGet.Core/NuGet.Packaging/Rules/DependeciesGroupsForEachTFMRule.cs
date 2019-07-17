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
        HashSet<NuGetFramework> _compatNotExactMatches = new HashSet<NuGetFramework>();
        HashSet<NuGetFramework> _noExactMatchesFromFile = new HashSet<NuGetFramework>();
        HashSet<NuGetFramework> _noExactMatchesFromNuspec = new HashSet<NuGetFramework>();

        public string MessageFormat { get; }

        public string SecondWarning { get; }

        public DependeciesGroupsForEachTFMRule(string messageFormat, string secondWarning)
        {
            MessageFormat = messageFormat;
            SecondWarning = secondWarning;
        }
        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var files = builder.GetFiles().
                Where(t => PathUtility.GetPathWithDirectorySeparator(t).Count(m => m == Path.DirectorySeparatorChar) > 1);
            var packageNuspec = builder.GetNuspec();
            return Validate(files, packageNuspec);
        }

        internal IEnumerable<PackagingLogMessage> Validate(IEnumerable<string> files, Stream packageNuspec)
        {
            var managedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
            var isCompatible = managedCodeConventions.Properties["tfm"].CompatibilityTest;
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

            List<NuGetFramework> tfmsFromNuspec = new List<NuGetFramework>();
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

            if (tfmsFromFiles.Count != 0 && tfmsFromNuspec.Count != 0)
            {
                foreach(var fileTFM in tfmsFromFiles)
                {
                    foreach(var nuspecTFM in tfmsFromNuspec)
                    {
                        if (!tfmsFromNuspec.Contains(fileTFM))
                        {
                            _noExactMatchesFromFile.Add(fileTFM);
                        }

                        if (!tfmsFromFiles.Contains(nuspecTFM))
                        {
                            _noExactMatchesFromNuspec.Add(nuspecTFM);

                            if (isCompatible(fileTFM, nuspecTFM))
                            {
                                _compatNotExactMatches.Add(fileTFM);
                            }
                        }
                    }
                }

                _noExactMatchesFromFile.RemoveWhere(IsCompat);
                _noExactMatchesFromNuspec.RemoveWhere(IsCompat);
                (string noExactMatchString, string compatMatchString) =
                    GenerateWarningString(_noExactMatchesFromFile, _noExactMatchesFromNuspec, _compatNotExactMatches);

                var issues = new List<PackagingLogMessage>();

                if (_noExactMatchesFromFile.Count != 0 || _noExactMatchesFromNuspec.Count != 0)
                {
                    issues.Add(PackagingLogMessage.CreateWarning(string.Format(MessageFormat,noExactMatchString),
                        NuGetLogCode.NU5128));
                }

                if (_compatNotExactMatches.Count != 0)
                {
                    issues.Add(PackagingLogMessage.CreateWarning(string.Format(SecondWarning, compatMatchString),
                        NuGetLogCode.NU5130));
                }

                if(issues.Count != 0)
                {
                    return issues;
                }
            }
            else
            if((tfmsFromFiles.Count != 0 && tfmsFromNuspec.Count == 0) || (tfmsFromFiles.Count == 0 && tfmsFromNuspec.Count != 0))
            {
                if(tfmsFromFiles.Count != 0)
                {
                    _noExactMatchesFromFile.AddRange(tfmsFromFiles);
                }

                if(tfmsFromNuspec.Count != 0)
                {
                    _noExactMatchesFromNuspec.AddRange(tfmsFromNuspec);
                }

                (string noExactMatchString, string compatMatchString) =
                    GenerateWarningString(_noExactMatchesFromFile, _noExactMatchesFromNuspec, _compatNotExactMatches);

                var issues = new List<PackagingLogMessage>();
                issues.Add(PackagingLogMessage.CreateWarning(string.Format(MessageFormat, noExactMatchString),
                        NuGetLogCode.NU5128));
                return issues;
            }

            return Array.Empty<PackagingLogMessage>();
        }

        private bool IsCompat(NuGetFramework tfm)
        {
            return _compatNotExactMatches.Contains(tfm);
        }

        private static IEnumerable<ContentItemGroup> GetContentForPattern(ContentItemCollection collection, PatternSet pattern)
        {
            return collection.FindItemGroups(pattern);
        }

        private static IEnumerable<NuGetFramework> GetGroupFrameworks(IEnumerable<ContentItemGroup> groups)
        {

            return groups.Select(e => (NuGetFramework)e.Properties["tfm"]);
        }

        private static (string, string) GenerateWarningString(HashSet<NuGetFramework> noExactMatchesFromFile,
                HashSet<NuGetFramework> noExactMatchesFromNuspec, HashSet<NuGetFramework> compatNotExactMatches)
        {
            string noExactMatchString = string.Empty;
            string compatMatchString = string.Empty;
            var beginning = "\n- Add ";
            if (noExactMatchesFromFile.Count != 0)
            {
                var ending = " to the nuspec";
                var firstElement = beginning + noExactMatchesFromFile.Select(t => t.GetFrameworkString()).First() + ending;
                noExactMatchString = noExactMatchesFromFile.Count > 1 ? beginning +
                    string.Join(ending + beginning,
                        noExactMatchesFromFile.Select(t => t.GetFrameworkString()).ToArray()) + ending :
                        firstElement;

            }
            if (noExactMatchesFromNuspec.Count != 0)
            {
                var ending = " to the lib or ref folder";
                var firstElement = beginning + noExactMatchesFromNuspec.Select(t => t.GetShortFolderName()).First() + ending;
                noExactMatchString = noExactMatchesFromNuspec.Count > 1 ? noExactMatchString + beginning +
                    string.Join(ending + beginning,
                        noExactMatchesFromNuspec.Select(t => t.GetShortFolderName()).ToArray()) + ending :
                    firstElement;
            }
            if (compatNotExactMatches.Count != 0)
            {
                var ending = " to the nuspec";
                var firstElement = beginning + compatNotExactMatches.Select(t => t.GetFrameworkString()).First() + ending;
                compatMatchString = compatNotExactMatches.Count > 1 ? beginning +
                    string.Join(ending + beginning,
                        compatNotExactMatches.Select(t => t.GetFrameworkString()).ToArray()) + ending :
                    firstElement;
            }
            return (noExactMatchString, compatMatchString);
        }
    }
}
