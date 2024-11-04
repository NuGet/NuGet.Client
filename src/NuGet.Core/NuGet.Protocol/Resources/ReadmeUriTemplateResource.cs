// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

#if NETCOREAPP
using System;
#endif

namespace NuGet.Protocol
{
    internal class ReadmeUriTemplateResource : INuGetResource
    {
        private readonly string _uriTemplate;

        public ReadmeUriTemplateResource(string uriTemplate)
        {
            _uriTemplate = uriTemplate;
        }

        /// <summary>
        /// Gets a URL for reporting package abuse. The URL will not be verified to exist.
        /// </summary>
        /// <param name="id">The package id (natural casing)</param>
        /// <param name="version">The package version</param>
        /// <returns>The first URL from the resource, with the URI template applied.</returns>
        public string GetReadmeUrl(string id, NuGetVersion version)
        {
            if (_uriTemplate == null)
            {
                return string.Empty;
            }

            var uriString = _uriTemplate
#if NETCOREAPP
               .Replace("{lower_id}", id.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
               .Replace("{lower_version}", version.ToNormalizedString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
#else
               .Replace("{lower_id}", id.ToLowerInvariant())
               .Replace("{lower_version}", version.ToNormalizedString().ToLowerInvariant());
#endif

            return uriString;
        }
    }
}
