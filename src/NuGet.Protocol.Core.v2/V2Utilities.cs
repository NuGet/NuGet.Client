// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v2
{
    public static class V2Utilities
    {
        public static async Task<bool> IsV2(Configuration.PackageSource source)
        {
            var url = new Uri(source.Source);

            // If the url is a directory, then it's a V2 source
            if (url.IsFile
                || url.IsUnc)
            {
                return !File.Exists(url.LocalPath);
            }
            var result = await GetContent(url);

            return true;
        }

        public static IPackageRepository GetV2SourceRepository(Configuration.PackageSource source)
        {
            var repo = new PackageRepositoryFactory().CreateRepository(source.Source);
            var _lprepo = repo as LocalPackageRepository;
            if (_lprepo != null)
            {
                return _lprepo;
            }
            var _userAgent = UserAgentUtil.GetUserAgent("NuGet.Client.Interop", "host");
            var events = repo as IHttpClientEvents;
            if (events != null)
            {
                events.SendingRequest += (sender, args) =>
                    {
                        var httpReq = args.Request as HttpWebRequest;
                        if (httpReq != null)
                        {
                            httpReq.UserAgent = _userAgent;
                        }
                    };
            }
            return repo;
        }

        public static async Task<string> GetContent(Uri address)
        {
            var client = new System.Net.Http.HttpClient(
                new HttpClientHandler() { UseDefaultCredentials = true });
            var response = await client.GetAsync(address);
            return await response.Content.ReadAsStringAsync();
        }

        public static NuGetVersion SafeToNuGetVer(SemanticVersion semanticVersion)
        {
            if (semanticVersion == null)
            {
                return null;
            }

            // Parse using the original version string to support non-normalized scenarios.
            return NuGetVersion.Parse(semanticVersion.ToString());
        }
    }
}
