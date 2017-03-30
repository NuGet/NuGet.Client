// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Test.Utility
{
    public class SimpleTestPackageContext
    {
        public SimpleTestPackageContext(string packageId)
        {
            Id = packageId;
        }

        public SimpleTestPackageContext(string packageId, string version)
        {
            Id = packageId;
            Version = version;
        }

        public SimpleTestPackageContext(PackageIdentity identity)
            : this (identity.Id, identity.Version.ToString())
        {
        }

        public SimpleTestPackageContext()
        {
        }

        public string Id { get; set; } = "packageA";
        public string Version { get; set; } = "1.0.0";
        public string MinClientVersion { get; set; }
        public List<SimpleTestPackageContext> Dependencies { get; set; } = new List<SimpleTestPackageContext>();
        public string Include { get; set; } = string.Empty;
        public string Exclude { get; set; } = string.Empty;
        // Used by the parent project
        public string PrivateAssets { get; set; } = string.Empty;
        public List<KeyValuePair<string, byte[]>> Files { get; set; } = new List<KeyValuePair<string, byte[]>>();
        public XDocument Nuspec { get; set; }
        public List<PackageType> PackageTypes { get; set; } = new List<PackageType>();

        /// <summary>
        /// runtime.json
        /// </summary>
        public string RuntimeJson { get; set; }

        public bool IsSymbolPackage { get; set; }

        public PackageIdentity Identity
        {
            get
            {
                return new PackageIdentity(Id, NuGetVersion.Parse(Version));
            }
        }

        /// <summary>
        /// Add a file to the zip. Ex: lib/net45/a.dll
        /// </summary>
        public void AddFile(string path)
        {
            AddFile(path, new byte[] { 0 });
        }

        public void AddFile(string path, string content)
        {
            AddFile(path, Encoding.UTF8.GetBytes(content));
        }

        public void AddFile(string path, byte[] bytes)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith("/") || path.IndexOf('\\') > -1)
            {
                throw new ArgumentException(nameof(path));
            }

            Files.Add(new KeyValuePair<string, byte[]>(path, bytes));
        }
    }
}
