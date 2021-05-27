// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    internal class PackageSourceSection
    {
        private readonly string _packageSourceKey;

        public string GetPackageSourceKey()
        {
            return _packageSourceKey;
        }

        private readonly string[] _nameSpaceIds;

        public string[] GetNameSpaceIds()
        {
            return _nameSpaceIds;
        }

        public PackageSourceSection(string packageSourceKey, string[] nameSpaceIds)
        {
            _nameSpaceIds = nameSpaceIds;
            _packageSourceKey = packageSourceKey;
        }
    }
}
