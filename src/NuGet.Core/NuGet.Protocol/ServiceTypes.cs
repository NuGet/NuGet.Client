// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, ClientVersions.Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    public static class ServiceTypes
    {
        public static readonly string[] SearchQueryService = { "SearchQueryService" + ClientVersions.Versioned, "SearchQueryService" + ClientVersions.Version340, "SearchQueryService" + ClientVersions.Version300beta };
        public static readonly string[] RegistrationsBaseUrl = RegistrationsBaseUrlTypes.RegistrationsBaseUrls;
        public static readonly string[] SearchAutocompleteService = { "SearchAutocompleteService" + ClientVersions.Versioned, "SearchAutocompleteService" + ClientVersions.Version300beta };
        public static readonly string[] ReportAbuse = { "ReportAbuseUriTemplate" + ClientVersions.Versioned, "ReportAbuseUriTemplate" + ClientVersions.Version300 };
        public static readonly string[] PackageDetailsUriTemplate = { "PackageDetailsUriTemplate" + ClientVersions.Version510 };
        public static readonly string[] LegacyGallery = { "LegacyGallery" + ClientVersions.Versioned, "LegacyGallery" + ClientVersions.Version200 };
        public static readonly string[] PackagePublish = { "PackagePublish" + ClientVersions.Versioned, "PackagePublish" + ClientVersions.Version200 };
        public static readonly string[] PackageBaseAddress = { "PackageBaseAddress" + ClientVersions.Versioned, "PackageBaseAddress" + ClientVersions.Version300 };
        public static readonly string[] RepositorySignatures = { "RepositorySignatures" + ClientVersions.Version500, "RepositorySignatures" + ClientVersions.Version490, "RepositorySignatures" + ClientVersions.Version470 };
        public static readonly string[] SymbolPackagePublish = { "SymbolPackagePublish" + ClientVersions.Version490 };
    }
}
