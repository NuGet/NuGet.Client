 // Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Core.v3
{
    public static class Types
    {
        public static readonly Uri PackageSearchResult = new Uri("http://schema.nuget.org/schema#PackageSearchResult");
        public static readonly Uri PackageIdentity = new Uri("http://schema.nuget.org/schema#PackageIdentity");
        public static readonly Uri PackageDescription = new Uri("http://schema.nuget.org/schema#PackageDescription");
        public static readonly Uri PackageLicensing = new Uri("http://schema.nuget.org/schema#PackageLicensing");
        public static readonly Uri PackageDependencies = new Uri("http://schema.nuget.org/schema#PackageDependencies");
        public static readonly Uri DependencyGroup = new Uri("http://schema.nuget.org/schema#DependencyGroup");
        public static readonly Uri Dependency = new Uri("http://schema.nuget.org/schema#Dependency");
        public static readonly Uri Stats = new Uri("http://schema.nuget.org/schema#Stats");
    }

    public static class ServiceTypes
    {
        public static readonly string Version200 = "/2.0.0";
        public static readonly string Version300beta = "/3.0.0-beta";
        public static readonly string Version300 = "/3.0.0";

        public static readonly string SearchQueryService = "SearchQueryService" + Version300beta;
        public static readonly string SearchAutocompleteService = "SearchAutocompleteService" + Version300beta;
        public static readonly string SearchGalleryQueryService = "SearchGalleryQueryService" + Version300beta;
        public static readonly string MetricsService = "MetricsService" + Version300beta;
        public static readonly string RegistrationsBaseUrl = "RegistrationsBaseUrl" + Version300beta;
        public static readonly string ReportAbuse = "ReportAbuseUriTemplate" + Version300beta;
        public static readonly string Stats = "Stats" + Version300beta;
        public static readonly string LegacyGallery = "LegacyGallery" + Version200;
        public static readonly string PackagePublish = "PackagePublish" + Version200;
        public static readonly string PackageBaseAddress = "PackageBaseAddress" + Version300;
    }

    public static class Properties
    {
        public static readonly string SubjectId = "@id";
        public static readonly string Type = "@type";

        public static readonly string PackageId = "id";
        public static readonly string Version = "version";
        public static readonly string Title = "title";
        public static readonly string Summary = "summary";
        public static readonly string Description = "description";
        public static readonly string Authors = "authors";
        public static readonly string Owners = "owners";
        public static readonly string IconUrl = "iconUrl";
        public static readonly string LicenseUrl = "licenseUrl";
        public static readonly string ProjectUrl = "projectUrl";
        public static readonly string Tags = "tags";
        public static readonly string DownloadCount = "totalDownloads";
        public static readonly string Published = "published";
        public static readonly string RequireLicenseAcceptance = "requireLicenseAcceptance";
        public static readonly string DependencyGroups = "dependencyGroups";
        public static readonly string LatestVersion = "latestVersion";
        public static readonly string TargetFramework = "targetFramework";
        public static readonly string Dependencies = "dependencies";
        public static readonly string Range = "range";
        public static readonly string MinimumClientVersion = "minClientVersion";
        public static readonly string Language = "language";
        public static readonly string PackageContent = "packageContent";
        public static readonly string Versions = "versions";
    }
}
