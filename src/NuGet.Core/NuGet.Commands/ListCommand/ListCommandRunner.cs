// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget list command
    /// </summary>
    public class ListCommandRunner : IListCommandRunner
    {
        /// <summary>
        /// Executes the logic for nuget list command.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPackageSearchMetadata> ExecuteCommand(ListArgs listArgs)

        {
            //TODO NK - Thisd needs to come in the list args
            CancellationToken token = CancellationToken.None;

            var resourceProviders = FactoryExtensionsV2.GetCoreV3(Repository.Provider);

            var resources = FactoryExtensionsV2.GetCoreV3(Repository.Provider).GetEnumerator();

            if( !(resources.Current.Value is ListResourceV2FeedResourceProvider))
            {
                resources.MoveNext();
            }

            var listResourceProvider = (ListResourceV2FeedResourceProvider) resources.Current.Value;

            IList<ListResourceV2Feed> SourceFeeds = new List<ListResourceV2Feed>();

            foreach (KeyValuePair<PackageSource,string> packageSource in listArgs.ListEndpoints)
            {
                SourceRepository sourceRepository = new SourceRepository(packageSource.Key,resourceProviders,FeedType.Undefined);
                ListResourceV2Feed feed =(ListResourceV2Feed) listResourceProvider.TryCreate(sourceRepository, token).Result.Item2; // TODO NK - Nasty
                if (feed == null) throw new ArgumentNullException(nameof(feed));
                SourceFeeds.Add(feed);
            }

            foreach (ListResourceV2Feed feed in SourceFeeds)
            {
                
                feed.ListAsync(listArgs.Arguments[0], listArgs.Prerelease, listArgs.AllVersions,
                    listArgs.IncludeDelisted, (ILogger) listArgs.Logger , token);
            }
            return null;
        }
    }
}