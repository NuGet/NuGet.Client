// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        private string _addToNuspecNoTfmFormat = AnalysisResources.ReferencesInNuspecAndRefFilesDontMatchWarningAddToNuspecNoTfmListItemFormat;
        public string MessageFormat => AnalysisResources.ReferencesInNuspecAndRefFilesDontMatchWarning;

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var refFiles = builder.GetFiles().Where(t => t.StartsWith(PackagingConstants.Folders.Ref));
            var nuspecReferences = GetReferencesFromNuspec(builder.GetNuspec());
            var missingItems = Compare(nuspecReferences, refFiles);
            return GenerateWarnings(missingItems);
        }

        internal IDictionary<string, IEnumerable<string>> GetReferencesFromNuspec(Stream nuspecStream)
        {
            var nuspecReferences = new Dictionary<string, IEnumerable<string>>();
            var packageNuspec = new NuspecReader(nuspecStream);
            var nuspec = packageNuspec.Xml;
            if (nuspec != null)
            {
                XNamespace name = nuspec.Root.Name.Namespace;
                var targetFrameworks = nuspec.Descendants(XName.Get("{" + name.NamespaceName + "}references")).Elements().Attributes("targetFramework");
                //if (targetFrameworks.Count() != 0)
                //{
                nuspecReferences = targetFrameworks.ToDictionary(k => NuGetFramework.Parse(k.Value).GetShortFolderName(), k => k.Parent.Elements().Attributes("file").Select(f => f.Value));
                //}
                //else
                //{
                var filesWithoutTFM = nuspec.Descendants(XName.Get("{" + name.NamespaceName + "}references")).Elements().Attributes("file").Select(f => f.Value);
                nuspecReferences.Add("any", filesWithoutTFM);
                //}
            }
            return nuspecReferences;
        }

        internal IEnumerable<MissingReference> Compare(IDictionary<string, IEnumerable<string>> nuspecReferences, IEnumerable<string> refFiles)
        {
            var missingItems = new List<MissingReference>();
            if (nuspecReferences.Count() != 0)
            {
                if (refFiles.Count() != 0)
                {
                    var filesByTFM = refFiles.Where(t => t.Count(m => m == '/') > 1).GroupBy(t => NuGetFramework.ParseFolder(t.Split('/')[1]).GetShortFolderName(), t => Path.GetFileName(t));
                    foreach (var files in filesByTFM)
                    {
                        if (files.Key == "unsupported")
                        {
                            continue;
                        }

                        string[] missingReferences;
                        string[] missingFiles;
                        IEnumerable<string> anyReferences = null;
                        if (nuspecReferences.TryGetValue(files.Key, out var currentReferences) || nuspecReferences.TryGetValue("any", out anyReferences))
                        {
                            if (anyReferences != null && currentReferences == null)
                            {
                                missingReferences = files.Where(m => !anyReferences.Contains(m)).ToArray();
                                missingFiles = anyReferences.Where(t => !files.Contains(t)).ToArray();
                            }
                            else
                            {
                                missingReferences = files.Where(m => !currentReferences.Contains(m)).ToArray();
                                missingFiles = currentReferences.Where(t => !files.Contains(t)).ToArray();
                            }
                        }
                        else
                        {
                            missingReferences = files.ToArray();
                            missingFiles = Array.Empty<string>();
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
                }
                else
                {
                    foreach (var item in nuspecReferences)
                    {
                        missingItems.Add(new MissingReference("ref", item.Key, item.Value.ToArray()));
                    }
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
                        message.AppendLine(string.Format(_addToRefFormat, item, file.Tfm));
                    }
                }

                foreach (var reference in referencesMissing)
                {
                    foreach (var item in reference.MissingItems)
                    {
                        if(reference.Tfm.Equals("any"))
                        {
                            message.AppendLine(string.Format(_addToNuspecNoTfmFormat, item));
                        }
                        else
                        {
                            message.AppendLine(string.Format(_addToNuspecFormat, item, reference.Tfm));
                        }
                    }
                }
                issues.Add(PackagingLogMessage.CreateWarning(message.ToString(), NuGetLogCode.NU5131));
            }
            return issues;
        }

        internal class MissingReference
        {
            public string MissingFrom { get; }

            public string Tfm { get; }

            public string[] MissingItems { get; }

            public MissingReference(string missingFrom, string tfm, string[] missingItems)
            {
                MissingFrom = missingFrom;
                Tfm = tfm;
                MissingItems = missingItems;
            }
        }
    }
}
