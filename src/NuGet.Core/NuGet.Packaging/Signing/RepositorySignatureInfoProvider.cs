// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System;

namespace NuGet.Packaging
{
    /// <summary>
    /// RepositorySignatureInfoProvdier is a static cache for repository signature information for package source.
    /// </summary>
    public class RepositorySignatureInfoProvider
    {
        private ConcurrentDictionary<string, RepositorySignatureInfo> _dict = new ConcurrentDictionary<string, RepositorySignatureInfo>();

        public static RepositorySignatureInfoProvider Instance { get; } = new RepositorySignatureInfoProvider();

        private RepositorySignatureInfoProvider()
        {
        }

        /// <summary>
        /// Try to get repository signature information for the source.
        /// </summary>
        /// <param name="source"></param>
        /// <returns>Null if can't find the repository signature information for the source.</returns>
        public RepositorySignatureInfo TryGetRepositorySignatureInfo(string source)
        {
            RepositorySignatureInfo repositorySignatureInfo = null;

            _dict.TryGetValue(source, out repositorySignatureInfo);

            return repositorySignatureInfo;
        }

        /// <summary>
        /// Add or update the repository signature information for the source.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="repositorySignatureInfo"></param>
        public void AddOrUpdateRepositorySignatureInfo(string source, RepositorySignatureInfo repositorySignatureInfo)
        {
            _dict[source] = repositorySignatureInfo ?? throw new ArgumentNullException(nameof(repositorySignatureInfo));
        }
    }
}
