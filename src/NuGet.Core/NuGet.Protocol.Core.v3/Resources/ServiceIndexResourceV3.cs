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
        private readonly IDictionary<string, List<Uri>> _index;
        private readonly DateTime _requestTime;
        private List<Uri> _empty;

        public ServiceIndexResourceV3(JObject index, DateTime requestTime)
        {
            _index = MakeLookup(index);
            _requestTime = requestTime;
        }

        private static IDictionary<string, List<Uri>> MakeLookup(JObject index)
        {
            var result = new Dictionary<string, List<Uri>>();

            JToken resources;
            if (index.TryGetValue("resources", out resources))
            {
                foreach (var resource in resources)
                {
                    JToken type = resource["@type"];
                    JToken id = resource["@id"];

                    if (type == null || id == null)
                    {
                        continue;
                    }

                    if (type.Type == JTokenType.Array)
                    {
                        foreach (var nType in type)
                        {
                            AddEndpoint(result, nType, id);
                        }
                    }
                    else
                    {
                        AddEndpoint(result, type, id);
                    }
                }
            }

            return result; 
        }

        private static void AddEndpoint(IDictionary<string, List<Uri>> result, JToken typeToken, JToken idToken)
        {
            string type = (string)typeToken;
            string id = (string)idToken;

            if (type == null || id == null)
            {
                return;
            }

            List<Uri> ids;
            if (!result.TryGetValue(type, out ids))
            {
                ids = new List<Uri>();
                result.Add(type, ids);
            }

            Uri uri;
            if (Uri.TryCreate(id, UriKind.Absolute, out uri))
            {
                ids.Add(new Uri(id));
            }
        }

        /// <summary>
        /// Time the index was requested
        /// </summary>
        public virtual DateTime RequestTime
        {
            get { return _requestTime; }
        }

        /// <summary>
        /// Empty set of endpoints - needed to efficiently meet indexer contract
        /// </summary>
        private List<Uri> Empty
        {
            get
            {
                if (_empty == null)
                {
                    _empty = new List<Uri>();
                }
                return _empty;
            }
        }

        /// <summary>
        /// A list of endpoints for a service type
        /// </summary>
        public virtual IReadOnlyList<Uri> this[string type]
        {
            get
            {
                List<Uri> endpoints;
                if (_index.TryGetValue(type, out endpoints))
                {
                    return endpoints;
                }
                return Empty;
            }
        }

        /// <summary>
        /// A list of endpoints for a service type - in priority order
        /// </summary>
        public virtual IReadOnlyList<Uri> this[string[] types]
        {
            get
            {
                foreach (var type in types)
                {
                    var result = this[type];
                    if (result.Count > 0)
                    {
                        return result;
                    }
                }
                return Empty;
            }
        }
    }
}
