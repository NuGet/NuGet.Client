// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Packaging.Build
{
    public class MetadataBuilder : Metadata
    {
        private readonly List<MetadataSection> _sections = new List<MetadataSection>();

        public void DefineSection(string name, string itemName, Action<MetadataSection> action)
        {
            var section = new MetadataSection
                {
                    Name = name,
                    ItemName = itemName,
                };

            action(section);
            _sections.Add(section);
        }

        public void DefineDependencies(Action<MetadataSection> action)
        {
            DefineSection("dependencies", "dependency", s =>
                {
                    s.GroupByProperty = "targetFramework";

                    action(s);
                });
        }

        public void DefineFrameworkAssemblies(Action<MetadataSection> action)
        {
            DefineSection("frameworkAssemblies", "frameworkAssembly", action);
        }

        public IEnumerable<MetadataSection> GetSections()
        {
            return _sections;
        }
    }
}
