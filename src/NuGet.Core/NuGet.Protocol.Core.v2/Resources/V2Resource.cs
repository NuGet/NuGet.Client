// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// Represents a resource provided by a V2 server. [ Like search resource, metadata resource]
    /// *TODOS: Add a trace source , Resource description ?
    /// </summary>
    public class V2Resource : INuGetResource
    {
        private IPackageRepository _v2Client;

        public V2Resource(V2Resource resource)
        {
            _v2Client = resource.V2Client;
        }

        public V2Resource(IPackageRepository repo)
        {
            _v2Client = repo;
        }

        public IPackageRepository V2Client
        {
            get { return _v2Client; }
        }
    }
}
