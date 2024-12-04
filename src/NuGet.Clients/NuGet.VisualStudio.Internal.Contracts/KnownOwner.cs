// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public class KnownOwner
    {
        public KnownOwner(string name, Uri link)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Link = link ?? throw new ArgumentNullException(nameof(link));
        }

        public string Name { get; }

        public Uri Link { get; }
    }
}
