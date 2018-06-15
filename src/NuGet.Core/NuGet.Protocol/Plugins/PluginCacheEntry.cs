// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Common;

namespace NuGet.Protocol.Plugins
{
    public sealed class PluginCacheEntry
    {
        public PluginCacheEntry(string rootCacheFolder, string pluginFilePath, string requestKey)
        {
            RootFolder = Path.Combine(rootCacheFolder, CachingUtility.RemoveInvalidFileNameChars(CachingUtility.ComputeHash(pluginFilePath)));
            CacheFileName = Path.Combine(RootFolder, CachingUtility.RemoveInvalidFileNameChars(requestKey) + ".dat");
            NewCacheFileName = CacheFileName + "-new";
        }

        internal TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);
        internal string CacheFileName { get; }
        private string RootFolder { get; }
        private string NewCacheFileName { get; }

        public IReadOnlyList<OperationClaim> OperationClaims { get; private set; }

        public void LoadFromFile()
        {
            Stream content = null;
            try
            {
                content = CachingUtility.ReadCacheFile(MaxAge, CacheFileName);
                if (content != null)
                {
                    ProcessContent(content);
                }
            }
            finally
            {
                content?.Dispose();
            }
        }

        private void ProcessContent(Stream content)
        {
            var serializer = new JsonSerializer();
            using (var sr = new StreamReader(content))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                OperationClaims = serializer.Deserialize<IReadOnlyList<OperationClaim>>(jsonTextReader);
            }
        }

        public void AddOrUpdateOperationClaims(IReadOnlyList<OperationClaim> operationClaims)
        {
            OperationClaims = operationClaims;
        }

        public async Task UpdateCacheFileAsync()
        {
            if (OperationClaims != null)
            {
                // Make sure the cache file directory is created before writing a file to it.
                DirectoryUtility.CreateSharedDirectory(RootFolder);

                // The update of a cached file is divided into two steps:
                // 1) Delete the old file.
                // 2) Create a new file with the same name.
                using (var fileStream = new FileStream(
                    NewCacheFileName,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    CachingUtility.BufferSize,
                    useAsync: true))
                {
                    var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(OperationClaims, Formatting.Indented));
                    await fileStream.WriteAsync(json, 0, json.Length);
                }

                if (File.Exists(CacheFileName))
                {
                    // Process B can perform deletion on an opened file if the file is opened by process A
                    // with FileShare.Delete flag. However, the file won't be actually deleted until A close it.
                    // This special feature can cause race condition, so we never delete an opened file.
                    if (!CachingUtility.IsFileAlreadyOpen(CacheFileName))
                    {
                        File.Delete(CacheFileName);
                    }
                }

                // If the destination file doesn't exist, we can safely perform moving operation.
                // Otherwise, moving operation will fail.
                if (!File.Exists(CacheFileName))
                {
                    File.Move(
                        NewCacheFileName,
                        CacheFileName);
                }
            }
        }
    }
}
