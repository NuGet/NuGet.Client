// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.SolutionRestoreManager.Test
{
    [Obsolete("Need to update to IVsProjectRestoreInfo3")]
    internal class VsReferenceItem : IVsReferenceItem
    {
        public string Name { get; }

        public IVsReferenceProperties Properties { get; }

        public VsReferenceItem(string name, IVsReferenceProperties properties)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Argument cannot be null or empty", nameof(name));
            }

            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            Name = name;
            Properties = properties;
        }
    }
}
