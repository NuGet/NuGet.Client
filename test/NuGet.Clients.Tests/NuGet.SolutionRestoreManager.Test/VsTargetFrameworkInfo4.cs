// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager.Test
{
    internal record VsTargetFrameworkInfo4 : IVsTargetFrameworkInfo4
    {
        public string TargetFrameworkMoniker { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<IVsReferenceItem2>> Items { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public VsTargetFrameworkInfo4(
            string targetFrameworkMoniker,
            IReadOnlyDictionary<string, IReadOnlyList<IVsReferenceItem2>> items,
            IReadOnlyDictionary<string, string> properties)
        {
            TargetFrameworkMoniker = !string.IsNullOrEmpty(targetFrameworkMoniker) ? targetFrameworkMoniker : throw new ArgumentException("Argument cannot be null or empty", nameof(targetFrameworkMoniker));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }
    }
}
