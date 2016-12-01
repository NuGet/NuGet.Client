// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Core.Types
{
    public class ListCommandResource : INuGetResource
    {
        private readonly string _listEndpoint;

        public ListCommandResource(string listEndpoint)
        {
            // _listEndpoint may be null
            _listEndpoint = listEndpoint;
        }

        public string GetListEndpoint()
        {
            return _listEndpoint;
        }
    }
}