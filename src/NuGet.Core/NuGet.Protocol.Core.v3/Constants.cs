// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Core.v3
{
    public static class ServiceTypes
    {
        public static readonly string Version200 = "/2.0.0";
        public static readonly string Version300beta = "/3.0.0-beta";
        public static readonly string Version300 = "/3.0.0";
        public static readonly string Version340 = "/3.4.0";

        public static readonly string[] SearchQueryService = { "SearchQueryService" + Version340, "SearchQueryService" + Version300beta };
        public static readonly string[] RegistrationsBaseUrl = { "RegistrationsBaseUrl" + Version340, "RegistrationsBaseUrl" + Version300beta };
        public static readonly string SearchAutocompleteService = "SearchAutocompleteService" + Version300beta;
        public static readonly string ReportAbuse = "ReportAbuseUriTemplate" + Version300;
        public static readonly string LegacyGallery = "LegacyGallery" + Version200;
        public static readonly string PackagePublish = "PackagePublish" + Version200;
        public static readonly string PackageBaseAddress = "PackageBaseAddress" + Version300;
    }
}
