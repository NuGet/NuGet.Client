// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.ProjectManagement
{
    public class XmlTransformer : IPackageFileTransformer
    {
        private readonly IDictionary<XName, Action<XElement, XElement>> _nodeActions;

        public XmlTransformer(IDictionary<XName, Action<XElement, XElement>> nodeActions)
        {
            _nodeActions = nodeActions;
        }

        public void TransformFile(Func<Stream> fileStreamFactory, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            // Get the xml fragment
            var xmlFragment = GetXml(fileStreamFactory, msBuildNuGetProjectSystem);

            var transformDocument = XmlUtility.GetOrCreateDocument(xmlFragment.Name, targetPath, msBuildNuGetProjectSystem);

            // Do a merge
            transformDocument.Root.MergeWith(xmlFragment, _nodeActions);

            MSBuildNuGetProjectSystemUtility.AddFile(msBuildNuGetProjectSystem, targetPath, transformDocument.Save);
        }

        public void RevertFile(Func<Stream> fileStreamFactory,
            string targetPath,
            IEnumerable<InternalZipFileInfo> matchingFiles,
            IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            // Get the xml snippet
            var xmlFragment = GetXml(fileStreamFactory, msBuildNuGetProjectSystem);

            var document = XmlUtility.GetOrCreateDocument(xmlFragment.Name,
                msBuildNuGetProjectSystem.ProjectFullPath,
                targetPath,
                msBuildNuGetProjectSystem.NuGetProjectContext);

            // Merge the other xml elements into one element within this xml hierarchy (matching the config file path)
            var mergedFragments = matchingFiles.Select(f => GetXml(f, msBuildNuGetProjectSystem))
                .Aggregate(new XElement(xmlFragment.Name), (left, right) => left.MergeWith(right, _nodeActions));

            // Take the difference of the xml and remove it from the main xml file
            document.Root.Except(xmlFragment.Except(mergedFragments));

            // Save the new content to the file system
            using (var fileStream = FileSystemUtility.CreateFile(msBuildNuGetProjectSystem.ProjectFullPath,
                targetPath, msBuildNuGetProjectSystem.NuGetProjectContext))
            {
                document.Save(fileStream);
            }
        }

        private static XElement GetXml(InternalZipFileInfo packageFileInfo, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            string content;
            using (var packageStream = File.OpenRead(packageFileInfo.ZipArchivePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                var zipArchivePackageEntry = PathUtility.GetEntry(zipArchive, packageFileInfo.ZipArchiveEntryFullName);
                if (zipArchivePackageEntry == null)
                {
                    throw new ArgumentException("internalZipFileInfo");
                }

                content = Preprocessor.Process(zipArchivePackageEntry.Open, msBuildNuGetProjectSystem);
            }
            return XElement.Parse(content, LoadOptions.PreserveWhitespace);
        }

        private static XElement GetXml(Func<Stream> fileStreamFactory, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            var content = Preprocessor.Process(fileStreamFactory, msBuildNuGetProjectSystem);
            return XElement.Parse(content, LoadOptions.PreserveWhitespace);
        }
    }
}
