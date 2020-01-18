// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Build.Tasks.Console;
using NuGet.Commands;

namespace NuGet.Build.Tasks.Console.Test
{
    internal class MockMSBuildProjectItem : IMSBuildProjectItem
    {
        private IDictionary<string, string> _metadata;
        private MSBuildItem _msbuildItem;
        private readonly string _identity;

        public MockMSBuildProjectItem(string identity, IDictionary<string, string> metadata)
        {
            _metadata = metadata;
            _msbuildItem = new MSBuildItem(identity, metadata);
            _identity = identity;
        }

        public string Identity => _msbuildItem.Identity;

        public IReadOnlyList<string> Properties => _msbuildItem.Properties;

        public void AddOrUpdateProperties(string name, string value)
        {
            if(_metadata.ContainsKey(name))
            {
                _metadata[name] = value;
            }
            else
            {
                _metadata.Add(name, value);
            }

            _msbuildItem = new MSBuildItem(_identity, _metadata);
        }

        public string GetProperty(string property)
        {
            return _msbuildItem.GetProperty(property);
        }

        public string GetProperty(string property, bool trim)
        {
            return _msbuildItem.GetProperty(property, trim);
        }
    }
}
