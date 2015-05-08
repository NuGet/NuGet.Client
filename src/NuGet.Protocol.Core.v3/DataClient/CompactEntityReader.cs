// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// CompactEntityReader makes an attempt at understanding the expanded entity and RDF concepts from the
    /// compacted form
    /// for scenarios where the graph is not available, or simple answers are needed.
    /// </summary>
    internal class CompactEntityReader
    {
        private readonly JObject _entityJson;
        private Uri _entityUri;
        private Uri _pageUri;

        public CompactEntityReader(JObject entityJson)
            : this(entityJson, null, null)
        {
        }

        /// <summary>
        /// Creates a new compact entity reader.
        /// </summary>
        /// <param name="entityJson">The JObject of the entity. Required.</param>
        /// <param name="entityUri">Optional, if the uri is already known it can be provided to save parsing it.</param>
        /// <param name="pageUri">Optional, if the uri is already known it can be provided to save parsing it.</param>
        public CompactEntityReader(JObject entityJson, Uri entityUri, Uri pageUri)
        {
            if (entityJson == null)
            {
                throw new ArgumentNullException("entityJson");
            }

            _entityJson = entityJson;
            _entityUri = entityUri;
            _pageUri = pageUri;
        }

        /// <summary>
        /// True if the entity id and the root id match, meaning this entity is complete.
        /// </summary>
        public bool? IsFromPage
        {
            get
            {
                bool? result = null;

                if (EntityUri != null)
                {
                    result = Utility.CompareRootUris(PageUri, EntityUri);
                }

                return result;
            }
        }

        /// <summary>
        /// Shortens the uris against the vocab and checks HasProperties
        /// </summary>
        public bool? HasPredicates(IEnumerable<Uri> predicates)
        {
            return HasProperties(predicates.Select(n => RemoveVocab(n, Vocab)));
        }

        /// <summary>
        /// True if the entity has all of these properties. This is assuming the proper vocabs are used.
        /// </summary>
        public bool? HasProperties(IEnumerable<string> desiredProperties)
        {
            var jsonProps = new HashSet<string>(_entityJson.Properties().Select(p => p.Name));
            return jsonProps.IsSupersetOf(desiredProperties);
        }

        /// <summary>
        /// @id of the page root
        /// </summary>
        public Uri PageUri
        {
            get
            {
                if (_pageUri == null)
                {
                    _pageUri = Utility.GetEntityUri(_entityJson.Root as JObject);
                }

                return _pageUri;
            }
        }

        /// <summary>
        /// @id of the entity
        /// </summary>
        public Uri EntityUri
        {
            get
            {
                if (_entityUri == null)
                {
                    _entityUri = Utility.GetEntityUri(_entityJson);
                }

                return _entityUri;
            }
        }

        /// <summary>
        /// @vocab used for this entity.
        /// </summary>
        public string Vocab
        {
            get
            {
                // TODO: remove this hardcoding
                return Constants.NuGetVocab;
            }
        }

        private static string RemoveVocab(Uri uri, string vocab)
        {
            var s = uri.AbsoluteUri;

            if (s.StartsWith(vocab, StringComparison.Ordinal))
            {
                s = s.Substring(vocab.Length, s.Length - vocab.Length);
            }

            return s;
        }
    }
}
