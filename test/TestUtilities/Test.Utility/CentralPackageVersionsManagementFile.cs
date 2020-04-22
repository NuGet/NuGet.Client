// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NuGet.Frameworks;

namespace NuGet.Test.Utility
{
    public class CentralPackageVersionsManagementFile
    {
        private const string Name = "Directory.Build.props";
        private string _path;

        private Dictionary<string, string> _packageVersions;

        private CentralPackageVersionsManagementFile(string path)
        {
            _path = path;
            _packageVersions = new Dictionary<string, string>();
        }

        public static CentralPackageVersionsManagementFile Create(string path)
        {
            return new CentralPackageVersionsManagementFile(path);
        }

        public CentralPackageVersionsManagementFile AddPackageVersion(string packageId, string packageVersion)
        {
            _packageVersions.Add(packageId, packageVersion);
            return this;
        }

        public CentralPackageVersionsManagementFile UpdatePackageVersion(string packageId, string packageVersion)
        {
            _packageVersions[packageId] = packageVersion;
            return this;
        }

        public CentralPackageVersionsManagementFile RemovePackageVersion(string packageId)
        {
            _packageVersions.Remove(packageId);
            return this;
        }

        public void Save()
        {
            XDocument cpvm = XDocument.Parse(
                "<Project>" +
                    "<PropertyGroup>" +
                        "<CentralPackageVersionsFileImported>true</CentralPackageVersionsFileImported>" +
                    "</PropertyGroup>" +
                "</Project>");
            NuGetFramework framework = null;
            foreach (var pv in _packageVersions)
            {
                ProjectFileUtils.AddItem(cpvm, "PackageVersion", pv.Key, framework: framework, properties: new Dictionary<string, string>(), new Dictionary<string, string>() { ["Version"] = pv.Value });
            }

            var filePath = Path.Combine(_path, Name);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            File.WriteAllText(filePath, cpvm.ToString());
        }
    }
}
