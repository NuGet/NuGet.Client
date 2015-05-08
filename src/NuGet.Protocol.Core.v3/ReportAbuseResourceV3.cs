// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    public class ReportAbuseResourceV3 : INuGetResource
    {
        public ReportAbuseResourceV3()
        {
        }

        //public ReportAbuseResourceV3(IEnumerable<Uri> reportAbuseTemplates)
        //{
        //    if (reportAbuseTemplates == null || !reportAbuseTemplates.Any())
        //    {
        //        throw new ArgumentNullException("reportAbuseTemplates");
        //    }

        //    _reportAbuseTemplates = reportAbuseTemplates;
        //}

        /// <summary>
        /// Gets a URL for reporting package abuse. The URL will not be verified to exist.
        /// </summary>
        /// <param name="id">The package id (natural casing)</param>
        /// <param name="version">The package version</param>
        /// <returns>The first URL from the resource, with the URI template applied.</returns>
        public Uri GetReportAbuseUrl(string id, NuGetVersion version)
        {
            //return Utility.ApplyPackageIdVersionToUriTemplate(_reportAbuseTemplates.First(), id, version);
            return new Uri(String.Format(CultureInfo.InvariantCulture, "https://www.nuget.org/packages/{0}/{1}/ReportAbuse", id, version.ToNormalizedString()));
        }

        /// <summary>
        /// Gets a URL for reporting package abuse. The URL will be tested for success with a HEAD request.
        /// </summary>
        /// <param name="id">The package id (natural casing)</param>
        /// <param name="version">The package version</param>
        /// <param name="cancellationToken">The cancellation token to terminate HTTP requests</param>
        /// <returns>The first URL available from the resource, with the URI template applied.</returns>
        //public async Task<Uri> GetReportAbuseUrl(string id, NuGetVersion version, CancellationToken cancellationToken)
        //{
        //    // REVIEW: maballia - doesn't this logic hit the first resource every time, not balancing to secondary resources unless the first is broken?
        //    foreach (Uri uri in Utility.ApplyPackageIdVersionToUriTemplate(_reportAbuseTemplates, id, version))
        //    {
        //        if (!cancellationToken.IsCancellationRequested)
        //        {
        //            // Get a new HttpClient each time because some BadRequest
        //            // responses were corrupting the HttpClient instance and
        //            // subsequent requests on it would hang unexpectedly
        //            // REVIEW: maballia - would this support proxy / auth scenarios? I guess we need a client that does support those, right?
        //            using (HttpClient http = new HttpClient())
        //            {
        //                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, uri);

        //                try
        //                {
        //                    HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        //                    if (response.IsSuccessStatusCode)
        //                    {
        //                        return uri;
        //                    }
        //                }
        //                catch
        //                {
        //                    // Any exception means we couldn't connect to the resource
        //                }
        //            }
        //        }
        //    }

        //    return null;
        //}
    }
}
