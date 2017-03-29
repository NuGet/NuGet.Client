// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    public static class ServiceTypes
    {
        public static readonly string Version200 = "/2.0.0";
        public static readonly string Version300beta = "/3.0.0-beta";
        public static readonly string Version300 = "/3.0.0";
        public static readonly string Version340 = "/3.4.0";
        public static readonly string Versioned = "/Versioned";

        public static readonly string[] SearchQueryService = { "SearchQueryService" + Versioned, "SearchQueryService" + Version340, "SearchQueryService" + Version300beta };
        public static readonly string[] RegistrationsBaseUrl = { "RegistrationsBaseUrl" + Versioned, "RegistrationsBaseUrl" + Version340, "RegistrationsBaseUrl" + Version300beta };
        public static readonly string[] SearchAutocompleteService = { "SearchAutocompleteService" + Versioned, "SearchAutocompleteService" + Version300beta };
        public static readonly string[] ReportAbuse = { "ReportAbuseUriTemplate" + Versioned, "ReportAbuseUriTemplate" + Version300 };
        public static readonly string[] LegacyGallery = { "LegacyGallery" + Versioned, "LegacyGallery" + Version200 };
        public static readonly string[] PackagePublish = { "PackagePublish" + Versioned, "PackagePublish" + Version200 };
        public static readonly string[] PackageBaseAddress = { "PackageBaseAddress" + Versioned, "PackageBaseAddress" + Version300 };
    }
}
