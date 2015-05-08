// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Stores/caches a service index json file.
    /// </summary>
    public class ServiceIndexResourceV3 : INuGetResource
    {
        private readonly JObject _index;
        private readonly DateTime _requestTime;

        public ServiceIndexResourceV3(JObject index, DateTime requestTime)
        {
            _index = index;
            _requestTime = requestTime;
        }

        /// <summary>
        /// Raw json
        /// </summary>
        public virtual JObject Index
        {
            get { return _index; }
        }

        /// <summary>
        /// Time the index was requested
        /// </summary>
        public virtual DateTime RequestTime
        {
            get { return _requestTime; }
        }

        /// <summary>
        /// A list of endpoints for a service type
        /// </summary>
        public virtual IReadOnlyList<Uri> this[string type]
        {
            get
            {
                return Index["resources"]
                    .Where(j =>
                        (j["@type"].Type == JTokenType.Array
                            ? j["@type"].Any(v => (string)v == type)
                            : ((string)j["@type"]) == type))
                    .Select(o => o["@id"].ToObject<Uri>())
                    .ToList();
            }
        }
    }
}
