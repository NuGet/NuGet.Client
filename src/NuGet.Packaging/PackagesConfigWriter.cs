// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    /// <summary>
    /// Writes the packages.config XML file to a stream
    /// </summary>
    public class PackagesConfigWriter : IDisposable
    {
        private readonly Stream _stream;
        private bool _disposed;
        private bool _closed;
        private NuGetVersion _minClientVersion;
        private IFrameworkNameProvider _frameworkMappings;
        private XDocument _xDocument;

        /// <summary>
        /// Create a packages.config writer
        /// </summary>
        /// <param name="stream">Stream to write the XML packages.config file into, or load existing packages.config from</param>
        /// <param name="createNew">Whether to create a new packages.config file, or load an existing one</param>
        public PackagesConfigWriter(Stream stream, bool createNew)
            : this(DefaultFrameworkNameProvider.Instance, stream, createNew)
        {
        }

        public PackagesConfigWriter(IFrameworkNameProvider frameworkMappings, Stream stream, bool createNew)
        {
            _stream = stream;
            _closed = false;
            _frameworkMappings = frameworkMappings;

            if (createNew)
            {
                _xDocument = new XDocument();
                EnsurePackagesNode();
            }
            // Load the existing packages.config file. 
            else
            {
                _xDocument = XDocument.Load(stream);
            }
        }

        /// <summary>
        /// Write a minimum client version to packages.config
        /// </summary>
        /// <param name="version">Minumum version of the client required to parse and use this file.</param>
        public void WriteMinClientVersion(NuGetVersion version)
        {
            if (_minClientVersion != null)
            {
                throw new PackagingException(string.Format(CultureInfo.CurrentCulture, Strings.MinClientVersionAlreadyExist));
            }

            _minClientVersion = version;

            var packagesNode = EnsurePackagesNode();

            if (_minClientVersion != null)
            {
                var minClientVersionAttribute = new XAttribute(XName.Get(PackagesConfig.MinClientAttributeName), _minClientVersion.ToNormalizedString());
                packagesNode.Add(minClientVersionAttribute);
            }
        }

        /// <summary>
        /// Add a package entry
        /// </summary>
        /// <param name="packageId">Package Id</param>
        /// <param name="version">Package Version</param>
        /// <param name="targetFramework">Package targetFramework that's compatible with current project</param>
        public void AddPackageEntry(string packageId, NuGetVersion version, NuGetFramework targetFramework)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (targetFramework == null)
            {
                throw new ArgumentNullException(nameof(targetFramework));
            }

            AddPackageEntry(new PackageIdentity(packageId, version), targetFramework);
        }

        /// <summary>
        /// Adds a basic package entry to the file
        /// </summary>
        public void AddPackageEntry(PackageIdentity identity, NuGetFramework targetFramework)
        {
            var entry = new PackageReference(identity, targetFramework);

            AddPackageEntry(entry);
        }

        /// <summary>
        /// Adds a package entry to the file
        /// </summary>
        /// <param name="entry">Package reference entry</param>
        public void AddPackageEntry(PackageReference entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (_disposed || _closed)
            {
                throw new PackagingException(string.Format(CultureInfo.CurrentCulture, Strings.UnableToAddEntry));
            }

            var packagesNode = EnsurePackagesNode();

            XElement package;
            if (PackagesConfig.HasAttributeValue(packagesNode, PackagesConfig.IdAttributeName, entry.PackageIdentity.Id, out package))
            {
                throw new PackagingException(String.Format(CultureInfo.CurrentCulture, 
                    Strings.PackageEntryAlreadyExist, entry.PackageIdentity.Id));
            }
            else
            {
                // Append the entry to existing package nodes
                var node = CreateXElementForPackageEntry(entry);
                packagesNode.Add(node);

                // Sort the entries by package Id
                SortPackageNodes(packagesNode);
            }
        }

        /// <summary>
        /// Update a package entry to the file
        /// </summary>
        /// <param name="entry">Package reference entry</param>
        public void UpdatePackageEntry(PackageReference oldEntry, PackageReference newEntry)
        {
            if (oldEntry == null)
            {
                throw new ArgumentNullException(nameof(oldEntry));
            }

            if (newEntry == null)
            {
                throw new ArgumentNullException(nameof(newEntry));
            }

            if (_disposed || _closed)
            {
                throw new PackagingException(string.Format(CultureInfo.CurrentCulture, Strings.UnableToAddEntry));
            }

            var packagesNode = _xDocument.Element(XName.Get(PackagesConfig.PackagesNodeName));

            // Check if package entry already exist on packages.config file
            var matchingNode = FindMatchingPackageNode(oldEntry, packagesNode);

            if (matchingNode == null)
            {
                throw new PackagingException(String.Format(CultureInfo.CurrentCulture, 
                    Strings.PackageEntryNotExist, oldEntry.PackageIdentity.Id, oldEntry.PackageIdentity.Version));
            }
            else
            {
                var newPackageNode = ReplacePackageAttributes(matchingNode, newEntry);
                matchingNode.ReplaceWith(newPackageNode);
            }
        }
        
        /// <summary>
        /// Remove a package entry
        /// </summary>
        /// <param name="packageId">Package Id</param>
        /// <param name="version">Package version</param>
        /// <param name="targetFramework">Package targetFramework</param>
        public void RemovePackageEntry(string packageId, NuGetVersion version, NuGetFramework targetFramework)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (targetFramework == null)
            {
                throw new ArgumentNullException(nameof(targetFramework));
            }

            RemovePackageEntry(new PackageIdentity(packageId, version), targetFramework);
        }

        /// <summary>
        /// Remove a package identity from the file
        /// </summary>
        /// <param name="identity">Package identity</param>
        /// <param name="targetFramework">Package targetFramework</param>
        public void RemovePackageEntry(PackageIdentity identity, NuGetFramework targetFramework)
        {
            var entry = new PackageReference(identity, targetFramework);

            RemovePackageEntry(entry);
        }

        /// <summary>
        /// Removes a package entry from the file
        /// </summary>
        /// <param name="entry">Package reference entry</param>
        public void RemovePackageEntry(PackageReference entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (_disposed || _closed)
            {
                throw new PackagingException(string.Format(CultureInfo.CurrentCulture, Strings.UnableToAddEntry));
            }

            var packagesNode = _xDocument.Element(XName.Get(PackagesConfig.PackagesNodeName));

            var matchingNode = FindMatchingPackageNode(entry, packagesNode);

            if (matchingNode == null)
            {
                throw new PackagingException(String.Format(CultureInfo.CurrentCulture, 
                    Strings.PackageEntryNotExist, entry.PackageIdentity.Id, entry.PackageIdentity.Version));
            }
            else
            {
                matchingNode.Remove();
            }
        }

        private XElement CreateXElementForPackageEntry(PackageReference entry)
        {
            var node = new XElement(XName.Get(PackagesConfig.PackageNodeName));

            node.Add(new XAttribute(XName.Get(PackagesConfig.IdAttributeName), entry.PackageIdentity.Id));
            node.Add(new XAttribute(XName.Get(PackagesConfig.VersionAttributeName), entry.PackageIdentity.Version));

            // map the framework to the short name
            // special frameworks such as any and unsupported will be ignored here
            if (entry.TargetFramework.IsSpecificFramework)
            {
                var frameworkShortName = entry.TargetFramework.GetShortFolderName(_frameworkMappings);

                if (!String.IsNullOrEmpty(frameworkShortName))
                {
                    node.Add(new XAttribute(XName.Get(PackagesConfig.TargetFrameworkAttributeName), frameworkShortName));
                }
            }

            if (entry.HasAllowedVersions)
            {
                node.Add(new XAttribute(XName.Get(PackagesConfig.allowedVersionsAttributeName), entry.AllowedVersions.ToString()));
            }

            if (entry.IsDevelopmentDependency)
            {
                node.Add(new XAttribute(XName.Get(PackagesConfig.developmentDependencyAttributeName), "true"));
            }

            if (entry.RequireReinstallation)
            {
                node.Add(new XAttribute(XName.Get(PackagesConfig.RequireInstallAttributeName), "true"));
            }

            return node;
        }

        private XElement EnsurePackagesNode()
        {
            var packagesNode = _xDocument.Element(XName.Get(PackagesConfig.PackagesNodeName));

            if (packagesNode == null)
            {
                packagesNode = new XElement(XName.Get(PackagesConfig.PackagesNodeName));
                _xDocument.Add(packagesNode);
            }

            return packagesNode;
        }

        private XElement FindMatchingPackageNode(PackageReference entry, XElement packagesNode)
        {
            XElement matchingIdNode;
            bool hasMatchingNode = PackagesConfig.HasAttributeValue(packagesNode, PackagesConfig.IdAttributeName, entry.PackageIdentity.Id, out matchingIdNode);

            if (matchingIdNode != null)
            {
                string version;
                PackagesConfig.TryGetAttribute(matchingIdNode, PackagesConfig.VersionAttributeName, out version);

                if (!string.IsNullOrEmpty(version))
                {
                    NuGetVersion nuGetVersion;
                    bool isNuGetVersion = NuGetVersion.TryParse(version, out nuGetVersion);

                    if (isNuGetVersion && nuGetVersion != null && nuGetVersion.Equals(entry.PackageIdentity.Version))
                    {
                        return matchingIdNode;
                    }
                }
            }

            return null;
        }

        private XElement ReplacePackageAttributes(XElement existingNode, PackageReference newEntry)
        {
            var newEntryNode = CreateXElementForPackageEntry(newEntry);

            var newAttributeNames = newEntryNode.Attributes().Select(a => a.Name);
            var existingAttributeNames = existingNode.Attributes().Select(a => a.Name);
            var addableAttributeNames = newAttributeNames.Except(existingAttributeNames);

            foreach (XName name in existingAttributeNames)
            {
                // Clear newValue
                string newValue = null;

                // Try to get newValue correlated to the attribute on the existing node.
                PackagesConfig.TryGetAttribute(newEntryNode, name.LocalName, out newValue);

                // When the attribute is not specified a value in the new node
                if (string.IsNullOrEmpty(newValue))
                {
                    if (!name.Equals(XName.Get(PackagesConfig.allowedVersionsAttributeName)))
                    {
                        // Remove the attribute, such as requirementReinstallation, developmentDependency and userInstalled.
                        existingNode.SetAttributeValue(name, null);
                    }
                    else
                    {
                        // no-op. Keep the allowedVersion attribute as-is.
                    }
                }
                else
                {
                    // Replace existing attributes with new values
                    existingNode.SetAttributeValue(name, newValue);
                }
            }

            // Add new attributes that was not in the old package reference entry, if any
            foreach (XName name in addableAttributeNames)
            {
                var attribute = new XAttribute(name, newEntryNode.Attribute(name).Value);
                existingNode.Add(attribute);
            }

            return existingNode;
        }

        private void SortPackageNodes(XElement packagesNode)
        {
            var newPackagesNode = new XElement(XName.Get(PackagesConfig.PackagesNodeName),
                from minClient in packagesNode.Attributes(XName.Get(PackagesConfig.MinClientAttributeName))
                select minClient,

                from package in packagesNode.Elements(XName.Get(PackagesConfig.PackageNodeName))
                orderby package.Attributes(XName.Get(PackagesConfig.IdAttributeName)).FirstOrDefault().Value
                select package);

            packagesNode.ReplaceWith(newPackagesNode);
        }

        private void WriteFile()
        {
            // Clear the content of the old stream
            _stream.Seek(0, SeekOrigin.Begin);
            _stream.SetLength(0);

            // Save the updated XDocument to the stream
            _xDocument.Save(_stream);
        }

        /// <summary>
        /// Write the file to the stream and close it to disallow further changes.
        /// </summary>
        public void Close()
        {
            if (!_closed)
            {
                _closed = true;

                WriteFile();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_closed)
            {
                Close();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Dispose(true);
            }
        }
    }
}
