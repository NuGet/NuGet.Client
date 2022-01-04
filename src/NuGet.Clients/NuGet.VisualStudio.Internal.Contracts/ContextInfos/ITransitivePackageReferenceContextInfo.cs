// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio.Internal.Contracts
{
    /// <summary>
    /// Represents a Package Reference with Transitive Origin information
    /// </summary>
    public interface ITransitivePackageReferenceContextInfo : IPackageReferenceContextInfo
    {
        IEnumerable<IPackageReferenceContextInfo> TransitiveOrigins { get; }
    }
}
