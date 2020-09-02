// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using MessagePack;
using Microsoft;

namespace NuGet.VisualStudio.Internal.Contracts
{
    [MessagePackObject(keyAsPropertyName: true)]
    public sealed class SearchResultContextInfo
    {
        public SearchResultContextInfo(IReadOnlyCollection<PackageSearchMetadataContextInfo> packageSearchItems, IDictionary<string, LoadingStatus> sourceLoadingStatus, bool hasMoreItems)
        {
            Assumes.NotNull(packageSearchItems);
            Assumes.NotNull(sourceLoadingStatus);

            PackageSearchItems = packageSearchItems;
            SourceLoadingStatus = sourceLoadingStatus;
            HasMoreItems = hasMoreItems;
        }

        public SearchResultContextInfo()
        {
            PackageSearchItems = new List<PackageSearchMetadataContextInfo>(0);
            SourceLoadingStatus = new Dictionary<string, LoadingStatus>();
        }

        public Guid? OperationId { get; set; }
        public bool HasMoreItems { get; set; }
        public IReadOnlyCollection<PackageSearchMetadataContextInfo> PackageSearchItems { get; }
        public IDictionary<string, LoadingStatus> SourceLoadingStatus { get; }
    }
}
