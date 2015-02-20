using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.ProjectManagement
{
    internal class XmlTransformer : IPackageFileTransformer
    {
        private readonly IDictionary<XName, Action<XElement, XElement>> _nodeActions;

        public XmlTransformer(IDictionary<XName, Action<XElement, XElement>> nodeActions)
        {
            _nodeActions = nodeActions;
        }
        public void TransformFile(ZipArchiveEntry packageFile, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            // Get the xml fragment
            XElement xmlFragment = GetXml(packageFile, msBuildNuGetProjectSystem);

            XDocument transformDocument = XmlUtility.GetOrCreateDocument(xmlFragment.Name, targetPath, msBuildNuGetProjectSystem);

            // Do a merge
            transformDocument.Root.MergeWith(xmlFragment, _nodeActions);

            MSBuildNuGetProjectSystemUtility.AddFile(msBuildNuGetProjectSystem, targetPath, transformDocument.Save);
        }

        public void RevertFile(ZipArchiveEntry packageFile, string targetPath, IEnumerable<InternalZipFileInfo> matchingFiles, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            // Get the xml snippet
            XElement xmlFragment = GetXml(packageFile, msBuildNuGetProjectSystem);

            XDocument document = XmlUtility.GetOrCreateDocument(xmlFragment.Name, msBuildNuGetProjectSystem.ProjectFullPath, targetPath, msBuildNuGetProjectSystem.NuGetProjectContext);

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
                var zipArchivePackageEntry = zipArchive.GetEntry(packageFileInfo.ZipArchiveEntryFullName);
                if(zipArchivePackageEntry == null)
                {
                    throw new ArgumentException("internalZipFileInfo");
                }

                content = Preprocessor.Process(zipArchivePackageEntry, msBuildNuGetProjectSystem);
            }
            return XElement.Parse(content, LoadOptions.PreserveWhitespace);
        }

        private static XElement GetXml(ZipArchiveEntry packageFile, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            var content = Preprocessor.Process(packageFile, msBuildNuGetProjectSystem);
            return XElement.Parse(content, LoadOptions.PreserveWhitespace);
        }
    }
}
