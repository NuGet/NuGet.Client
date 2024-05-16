// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager.Test
{
    internal class VsReferenceItem2 : IVsReferenceItem2
    {
        public string Name { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public VsReferenceItem2(string name, IReadOnlyDictionary<string, string> properties)
        {
            Name = !string.IsNullOrEmpty(name) ? name : throw new ArgumentException("Argument cannot be null or empty", nameof(name));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }
    }
}
