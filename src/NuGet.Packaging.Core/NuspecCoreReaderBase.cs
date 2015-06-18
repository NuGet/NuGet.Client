// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Versioning;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// A very basic Nuspec reader that understands the Id, Version, PackageType, and MinClientVersion of a package.
    /// </summary>
    public abstract class NuspecCoreReaderBase : INuspecCoreReader
    {
        private static readonly Version _emptyVersion = new Version(0, 0);
        private readonly XDocument _xml;
        private XElement _metadataNode;

        protected const string Metadata = "metadata";
        protected const string Id = "id";
        protected const string Version = "version";
        protected const string MinClientVersion = "minClientVersion";
        protected const string DevelopmentDependency = "developmentDependency";
        protected const string PackageType = "packageType";
        protected const string PackageTypeVersion = "version";

        /// <summary>
        /// Read a nuspec from a stream.
        /// </summary>
        public NuspecCoreReaderBase(Stream stream)
            : this(XDocument.Load(stream))
        {
        }

        /// <summary>
        /// Reads a nuspec from XML
        /// </summary>
        public NuspecCoreReaderBase(XDocument xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException("xml");
            }

            _xml = xml;
        }

        /// <summary>
        /// Id of the package
        /// </summary>
        public string GetId()
        {
            var node = MetadataNode.Elements(XName.Get(Id, MetadataNode.GetDefaultNamespace().NamespaceName)).FirstOrDefault();
            return node == null ? null : node.Value;
        }

        /// <summary>
        /// Version of the package
        /// </summary>
        public NuGetVersion GetVersion()
        {
            var node = MetadataNode.Elements(XName.Get(Version, MetadataNode.GetDefaultNamespace().NamespaceName)).FirstOrDefault();
            return node == null ? null : NuGetVersion.Parse(node.Value);
        }

        /// <summary>
        /// The minimum client version this package supports.
        /// </summary>
        public NuGetVersion GetMinClientVersion()
        {
            var node = MetadataNode.Attribute(XName.Get(MinClientVersion));
            return node == null ? null : NuGetVersion.Parse(node.Value);
        }

        public PackageType GetPackageType()
        {
            var node = MetadataNode.Element(XName.Get(PackageType, MetadataNode.GetDefaultNamespace().NamespaceName));
            if (node != null)
            {
                var versionAttribute = node.Attribute(XName.Get(PackageTypeVersion));
                var packageTypeVersion = versionAttribute == null ? _emptyVersion : System.Version.Parse(versionAttribute.Value);
                return new PackageType(node.Value, packageTypeVersion);
            }

            return Core.PackageType.Default;
        }

        /// <summary>
        /// The developmentDependency attribute
        /// </summary>
        public bool GetDevelopmentDependency()
        {
            var node = MetadataNode.Elements(XName.Get(DevelopmentDependency, MetadataNode.GetDefaultNamespace().NamespaceName)).FirstOrDefault();
            return node == null ? false : bool.Parse(node.Value);
        }

        /// <summary>
        /// Nuspec Metadata
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> GetMetadata()
        {
            // Remove the PackageType element prior to returning the resulting metadata.
            var filteredMetadataElements = MetadataNode.Elements().Where(
                element => !element.HasElements &&
                !String.IsNullOrEmpty(element.Value) &&
                !element.Name.LocalName.Equals(PackageType, StringComparison.OrdinalIgnoreCase));


            return filteredMetadataElements
                .Select(element => new KeyValuePair<string, string>(element.Name.LocalName, element.Value));
        }

        protected XElement MetadataNode
        {
            get
            {
                if (_metadataNode == null)
                {
                    // find the metadata node regardless of the NS, some legacy packages have the NS here instead of on package
                    _metadataNode = _xml.Root.Elements().Where(e => StringComparer.Ordinal.Equals(e.Name.LocalName, Metadata)).FirstOrDefault();

                    if (_metadataNode == null)
                    {
                        throw new PackagingException(Strings.FormatMissingMetadataNode(Metadata));
                    }
                }

                return _metadataNode;
            }
        }

        /// <summary>
        /// Raw XML doc
        /// </summary>
        public XDocument Xml
        {
            get { return _xml; }
        }

        public PackageIdentity GetIdentity()
        {
            return new PackageIdentity(GetId(), GetVersion());
        }
    }
}
