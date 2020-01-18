// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Build.Tasks.Console;
using NuGet.Commands;
using NuGet.Test.Utility;

namespace NuGet.Build.Tasks.Test
{
    internal class MockMSBuildProject : MSBuildItem, IMSBuildProject
    {
        public MockMSBuildProject(TestDirectory directory)
            : this(Path.Combine(directory, "ProjectA.csproj"))
        {
        }

        public MockMSBuildProject(TestDirectory directory, IDictionary<string, string> properties)
            : this(Path.Combine(directory, "ProjectA.csproj"), properties)
        {
        }

        public MockMSBuildProject(string fullPath)
            : this(fullPath, properties: null)
        {
        }

        public MockMSBuildProject(IDictionary<string, string> properties)
            : this("ProjectA", properties)
        {
        }

        public MockMSBuildProject(string fullPath, IDictionary<string, string> properties)
            : this(fullPath, properties, items: null)
        {
        }

        public MockMSBuildProject(string fullPath, IDictionary<string, string> properties, IDictionary<string, IList<IMSBuildProjectItem>> items)
            : base(Path.GetFileName(fullPath), properties ?? new Dictionary<string, string>())
        {
            Items = items ?? new Dictionary<string, IList<IMSBuildProjectItem>>();

            Directory = Path.GetDirectoryName(fullPath);

            FullPath = fullPath;
        }

        public string Directory { get; }

        public string FullPath { get; }

        public IDictionary<string, IList<IMSBuildProjectItem>> Items { get; set; }

        public IEnumerable<IMSBuildProjectItem> GetItems(string name)
        {
            return Items.TryGetValue(name, out var items) ? items : null;
        }
    }
}
