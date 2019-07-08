using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace NuGet.Packaging.Rules
{
    class ReferencesInNuspecMatchRefAssetsRule : IPackageRule
    {
        private string _addToRefFormat = AnalysisResources.ReferencesInNuspecAndRefFilesDontMatchWarningAddToRefListItemFormat;
        private string _addToNuspecFormat = AnalysisResources.ReferencesInNuspecAndRefFilesDontMatchWarningAddToNuspecListItemFormat;
        public string MessageFormat => AnalysisResources.ReferencesInNuspecAndRefFilesDontMatchWarning;

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var refFiles = builder.GetFiles().Where(t => t.StartsWith(PackagingConstants.Folders.Ref));
            var nuspecReferences = GetReferencesFromNuspec(builder.GetNuspec());
            var missingItems = Compare(nuspecReferences, refFiles);
            return GenerateWarnings(missingItems);
        }

        internal IDictionary<string, string[]> GetReferencesFromNuspec(Stream nuspecStream)
        {
            var nuspecReferences = new Dictionary<string, string[]>();
            var packageNuspec = new NuspecReader(nuspecStream);
            var nuspec = packageNuspec.Xml;
            if (nuspec != null)
            {
                XNamespace name = nuspec.Root.Name.Namespace;
                var keys = nuspec.Descendants(XName.Get("{" + name.NamespaceName + "}references")).Elements().Attributes("targetFramework");
                var shortName = keys.Select(t => NuGetFramework.Parse(t.Value).GetShortFolderName());
                nuspecReferences = shortName.Zip(keys.Select(t => t.Parent.Elements().Attributes("file").Select(m => m.Value)), (tfm, refFiles) => new { tfm, refFiles = refFiles.ToArray() })
                    .ToDictionary(val => val.tfm, val => val.refFiles);
            }
            return nuspecReferences;
        }

        internal IEnumerable<MissingReference> Compare(IDictionary<string, string[]> nuspecReferences, IEnumerable<string> refFiles)
        {
            var filesByTFM = refFiles.Where(t => t.Count(m => m == '/') > 1).GroupBy(t => NuGetFramework.ParseFolder(t.Split('/')[1]).GetShortFolderName(), t => GetFile(t));
            var missingItems = new List<MissingReference>();

            foreach (var files in filesByTFM)
            {
                if(files.Key == "unsupported")
                {
                    continue;
                }

                var missingFiles = Array.Empty<string>();
                var missingReferences = Array.Empty<string>();    
                if (nuspecReferences.TryGetValue(files.Key, out var currentReferences))
                {
                    missingReferences = files.Where(m => !currentReferences.Contains(m)).ToArray();
                    missingFiles = currentReferences.Where(t => !files.Contains(t)).ToArray();
                }
                else
                {
                    missingReferences = files.ToArray();
                }

                if (missingFiles.Length != 0)
                {
                    missingItems.Add(new MissingReference("ref", files.Key, missingFiles));
                }

                if (missingReferences.Length != 0)
                {
                    missingItems.Add(new MissingReference("nuspec", files.Key, missingReferences));
                }
            }
            return missingItems;
        }

        internal IEnumerable<PackagingLogMessage> GenerateWarnings(IEnumerable<MissingReference> missingItems)
        {
            var issues = new List<PackagingLogMessage>();
            if (missingItems.Count() != 0)
            {
                var message = new StringBuilder();
                message.AppendLine(MessageFormat);
                var referencesMissing = missingItems.Where(t => t.MissingFrom == "nuspec");
                var refFilesMissing = missingItems.Where(t => t.MissingFrom == "ref");
                foreach (var file in refFilesMissing)
                {
                    foreach(var item in file.MissingItems)
                    {
                        message.AppendLine(string.Format(_addToRefFormat, item, file.TFM));
                    }
                }

                foreach (var reference in referencesMissing)
                {
                    foreach (var item in reference.MissingItems)
                    {
                        message.AppendLine(string.Format(_addToNuspecFormat, item, reference.TFM));
                    }
                }
                issues.Add(PackagingLogMessage.CreateWarning(message.ToString(), NuGetLogCode.NU5131));
            }
            return issues;
        }

        private string GetFile(string filePath)
        {
            return filePath.Split('/')[filePath.Count(p => p == '/')];
        }


        internal class MissingReference
        {
            public string MissingFrom { get; }

            public string TFM { get; }

            public string[] MissingItems { get; }

            public MissingReference(string missingFrom, string tfm, string[] missingItems)
            {
                MissingFrom = missingFrom;
                TFM = tfm;
                MissingItems = missingItems;
            }
        }
    }
}
