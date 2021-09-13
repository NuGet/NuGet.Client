// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using ContractsItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class HyperlinkClickedTelemetryEvent : TelemetryEvent
    {
        internal const string HyperlinkClickedEventName = "HyperlinkClicked";
        internal const string AlternativePackageIdPropertyName = "AlternativePackageId";
        internal const string HyperLinkTypePropertyName = "HyperlinkType";
        internal const string CurrentTabPropertyName = "CurrentTab";
        internal const string IsSolutionViewPropertyName = "IsSolutionView";

        internal string SelectedSourceHost
        {
            set => AddPiiData(nameof(SelectedSourceHost), value);
        }

        internal UriHostNameType SelectedSourceHostNameType
        {
            set => base[nameof(SelectedSourceHostNameType)] = value;
        }

        internal SourceFeedType SelectedSourceType
        {
            set => base[nameof(SelectedSourceType)] = value;
        }

        internal bool HasUnknownRemoteFeed
        {
            set => base[nameof(HasUnknownRemoteFeed)] = value;
        }

        public HyperlinkClickedTelemetryEvent(HyperlinkType hyperlinkType, ContractsItemFilter currentTab, bool isSolutionView)
            : base(HyperlinkClickedEventName)
        {
            base[HyperLinkTypePropertyName] = hyperlinkType;
            base[CurrentTabPropertyName] = currentTab;
            base[IsSolutionViewPropertyName] = isSolutionView;
        }

        public HyperlinkClickedTelemetryEvent(HyperlinkType hyperlinkType, ContractsItemFilter currentTab, bool isSolutionView, string alternativePackageId, PackageSourceMoniker selectedSource)
            : this(hyperlinkType, currentTab, isSolutionView)
        {
            if (selectedSource == null)
            {
                throw new ArgumentNullException(nameof(selectedSource));
            }

            var feedInfo = GetFeedInfo(selectedSource);
            AddPiiData(AlternativePackageIdPropertyName, VSTelemetryServiceUtility.NormalizePackageId(alternativePackageId));
            SelectedSourceHost = feedInfo.Item3;
            SelectedSourceHostNameType = feedInfo.Item2;
            SelectedSourceType = feedInfo.Item1;
            HasUnknownRemoteFeed = ContainsUnknownRemoteFeed(selectedSource);
        }

        internal static bool ContainsUnknownRemoteFeed(PackageSourceMoniker selectedSource)
        {
            return selectedSource.PackageSources
                .Where(srcCtx => srcCtx.IsEnabled)
                .Select(src => new PackageSource(src.Source, src.Name, src.IsEnabled))
                .Where(src => src.IsHttp)
                .Any(src => PackageSourceTelemetry.GetMsFeed(src) != null);
        }

        internal static (SourceFeedType, UriHostNameType, string) GetFeedInfo(PackageSourceMoniker selectedSource)
        {
            if (selectedSource == null || selectedSource.PackageSources.Count == 0)
            {
                return (SourceFeedType.Unknown, UriHostNameType.Unknown, selectedSource?.SourceName ?? "Unknown");
            }

            if (selectedSource.IsAggregateSource)
            {
                return (SourceFeedType.MultiFeed, UriHostNameType.Unknown, selectedSource.SourceName);
            }

            var ctxSource = selectedSource.PackageSources.First();
            var src = new PackageSource(ctxSource.Source, ctxSource.Name, ctxSource.IsEnabled);
            var uri = src.TrySourceAsUri;
            var myFeedType = SourceFeedType.Unknown;
            var hostType = UriHostNameType.Unknown;
            var hostPart = selectedSource.SourceName;

            if (uri != null)
            {
                hostType = uri.HostNameType;
                hostPart = uri.Host;
                if (uri.IsUnc)
                {
                    myFeedType = SourceFeedType.Unc;
                }
                else if (src.IsLocal)
                {
                    myFeedType = SourceFeedType.LocalAbsolute;
                }
                else if (src.IsHttp)
                {
                    myFeedType = SourceFeedType.Http;
                }
            }
            else if (!string.IsNullOrEmpty(src.Source))
            {
                myFeedType = SourceFeedType.LocalRelative;
                hostPart = src.Source;
            }

            return (myFeedType, hostType, hostPart);
        }
    }

    internal enum SourceFeedType
    {
        Http,
        LocalAbsolute,
        LocalRelative,
        Unc,
        MultiFeed,
        Unknown,
    }
}
