// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.Protocol
{
    public static class HttpResponseMessageExtensions
    {
        // Maximum limit we try to read from http stream, 20MB. Maybe we can read this value from NugGet.Config file to override it.
        internal const int MaxBytesToRead = 20 * 1048576;

        public static void LogServerWarning(this HttpResponseMessage response, ILogger log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.Headers.Contains(ProtocolConstants.ServerWarningHeader))
            {
                foreach (var warning in response.Headers.GetValues(ProtocolConstants.ServerWarningHeader))
                {
                    log.LogWarning(warning);
                }
            }
        }


        internal static async Task<IEnumerable<JToken>> AsJObjectTakeLimitedAsync<T>(this HttpResponseMessage httpInitialResponse, CancellationToken token)
        {
            if (httpInitialResponse == null)
            {
                return null;
            }

            var rawData = new List<byte>();
            var result = new List<JToken>();
            var isMoreToRead = true;

            using (var stream = await httpInitialResponse.Content.ReadAsStreamAsync())
            {
                var totalRead = 0L;
                var buffer = new byte[262144]; // 2^18 = 256KB, it should be enough for 99.9% request. It'll go out this buffer nuget server is rogue or something is wrong on server side.

                do
                {
                    token.ThrowIfCancellationRequested();

                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                    if (read == 0)
                    {
                        isMoreToRead = false;
                    }
                    else
                    {
                        var actualData = new byte[read];
                        buffer.ToList().CopyTo(0, actualData, 0, read);
                        rawData.AddRange(actualData);
                        totalRead += read;

                        // There some rogue server not honoring our parameters and returning everything which is more than 100MB data.
                        // We need to stop somepoint otherwise some regue server can return gigabytes data that always causes memory overflow or application starts not responding.
                        // Put hard stop at MaxBytesToRead since it can be done with under 100KB data (See MaxBytesToRead).
                        if (totalRead >= MaxBytesToRead)
                        {
                            break;
                        }
                    }
                } while (isMoreToRead);
            }

            if (!isMoreToRead && rawData.Any())
            {
                result.AddRange(ProcessFullStreamData(rawData));
            }
            else if (isMoreToRead && rawData.Any())
            {
                result.AddRange(ProcessPartialStream(rawData));
            }

            return result;
        }


        private static IEnumerable<JToken> ProcessFullStreamData(List<byte> rawData)
        {
            string jsonStr = Encoding.UTF8.GetString(rawData.ToArray());
            var obj = JsonConvert.DeserializeObject<JObject>(jsonStr);
            return obj[JsonProperties.Data] as JArray ?? Enumerable.Empty<JToken>();
        }

        private static IEnumerable<JToken> ProcessPartialStream(List<byte> rawData)
        {
            if (rawData.Count < MaxBytesToRead)
            {
                throw new ArgumentException($"rawData should be more than {MaxBytesToRead}. This method is only designed for huge Http stream read (See MaxBytesToRead).");
            }

            string jsonStr = Encoding.UTF8.GetString(rawData.Skip(rawData.Count/2).ToArray());
            
            var lastValidTag = jsonStr.LastIndexOf(".json\"}]},{\"@id\":\"", StringComparison.Ordinal);

            // There should be at least one valid closing tag before our cut off due to MaxBytesToRead limit since json string is at least 10 MB.
            if (lastValidTag < 0)
            {
                lastValidTag = jsonStr.LastIndexOf("\"},{\"@id\":\"", StringComparison.Ordinal);
                if (lastValidTag < 0)
                {
                    throw new InvalidDataException($"Not proper json, close tag not found");
                }
                else
                {
                    // Properly trim by last good nuget package.
                    jsonStr = jsonStr.Substring(0, lastValidTag);
                    //Add back proper closing tag.
                    jsonStr += ".json\"}]}]}";
                }
            }
            else
            {
                // Properly trim by last good nuget package.
                jsonStr = jsonStr.Substring(0, lastValidTag);
                //Add back proper closing tag.
                jsonStr += ".json\"}]}]}";
            }

            var obj = JsonConvert.DeserializeObject<JObject>(jsonStr);
            var data1 = obj[JsonProperties.Data] as JArray ?? Enumerable.Empty<JToken>();
            var temp = data1.OfType<JObject>();
            return temp;
        }

    }
}
