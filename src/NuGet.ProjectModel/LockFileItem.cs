// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.ProjectModel
{
    public class LockFileItem
    {
        public LockFileItem(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public override string ToString() => Path;
    }
}
