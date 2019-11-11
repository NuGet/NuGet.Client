// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Utility;

namespace NuGet.VisualStudio.Telemetry
{
    public sealed class PackageSourceTelemetry : IDisposable
    {
        private ConcurrentDictionary<string, Data> _data;
        private List<SourceRepository> _sources;
        private Guid _parentId;

        public PackageSourceTelemetry(List<SourceRepository> sources, Guid parentId)
        {
            _data = new ConcurrentDictionary<string, Data>();
            ProtocolDiagnostics.Event += ProtocolDiagnostics_Event;
            _sources = sources;
            _parentId = parentId;
        }

        private void ProtocolDiagnostics_Event(ProtocolDiagnosticEvent pdEvent)
        {
            AddAggregateData(pdEvent, _data);
        }

        public static void AddAggregateData(ProtocolDiagnosticEvent pdEvent, ConcurrentDictionary<string, Data> allData)
        {
            var data = allData.GetOrAdd(pdEvent.Source, _ => new Data());

            var resourceData = pdEvent.Url.OriginalString.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                ? data.Nupkg
                : data.Metadata;

            lock (resourceData.Lock)
            {
                ApplyTiming(resourceData.EventTiming, pdEvent.EventDuration);

                if (pdEvent.HeaderDuration.HasValue)
                {
                    if (resourceData.HeaderTiming == null)
                    {
                        resourceData.HeaderTiming = new ResourceTimingData();
                    }

                    ApplyTiming(resourceData.HeaderTiming, pdEvent.HeaderDuration.Value);
                }

                if (pdEvent.IsSuccess)
                {
                    resourceData.Successful++;
                }

                if (pdEvent.IsRetry)
                {
                    resourceData.Retries++;
                }

                if (pdEvent.IsCancelled)
                {
                    resourceData.Cancelled++;
                }

                if (pdEvent.IsLastAttempt && !pdEvent.IsSuccess)
                {
                    resourceData.Failed++;
                }

                if (pdEvent.Bytes > 0)
                {
                    resourceData.TotalBytes += pdEvent.Bytes;
                    if (pdEvent.Bytes > resourceData.MaxBytes)
                    {
                        resourceData.MaxBytes = pdEvent.Bytes;
                    }
                }

                if (pdEvent.HttpStatusCode.HasValue)
                {
                    if (!resourceData.StatusCodes.TryGetValue(pdEvent.HttpStatusCode.Value, out var count))
                    {
                        count = 0;
                    }
                    resourceData.StatusCodes[pdEvent.HttpStatusCode.Value] = count + 1;
                }
            }
        }

        private static void ApplyTiming(ResourceTimingData timingData, TimeSpan duration)
        {
            timingData.Requests++;
            timingData.TotalDuration += duration;

            if (timingData.MinDuration > duration)
            {
                timingData.MinDuration = duration;
            }

            if (timingData.MaxDuration < duration)
            {
                timingData.MaxDuration = duration;
            }
        }

        public void Dispose()
        {
            ProtocolDiagnostics.Event -= ProtocolDiagnostics_Event;

            var parentId = _parentId.ToString();
            foreach (var kvp in _data)
            {
                Data data = kvp.Value;
                string source = kvp.Key;
                SourceRepository sourceFeed = _sources.FirstOrDefault(s => s.PackageSource.SourceUri?.OriginalString == source);

                var telemetry = ToTelemetry(data, source, sourceFeed, parentId);

                TelemetryActivity.EmitTelemetryEvent(telemetry);
            }
       }

        internal static TelemetryEvent ToTelemetry(Data data, string source, SourceRepository sourceFeed, string parentId)
        {
            if (data.Metadata.EventTiming.Requests == 0 || data.Nupkg.EventTiming.Requests == 0)
            {
                return null;
            }

            var telemetry = new TelemetryEvent("PackageSourceDiagnostics",
                new Dictionary<string, object>()
                {
                    { "ParentId", parentId },
                });

            // source info
            telemetry.AddPiiData("source.url", source);

            if (sourceFeed != null)
            {
                var packageSource = sourceFeed.PackageSource;
                telemetry["source.type"] = packageSource.IsHttp ? "http" : packageSource.IsLocal ? "local" : "unknown";
                telemetry["source.protocol"] = packageSource.ProtocolVersion;

                var msFeed = GetMsFeed(packageSource);
                if (msFeed != null)
                {
                    telemetry["source.msfeed"] = msFeed;
                }
            }

            // metadata
            lock (data.Metadata.Lock)
            {
                telemetry["metadata.requests"] = data.Metadata.EventTiming.Requests;
                telemetry["metadata.success"] = data.Metadata.Successful;
                telemetry["metadata.retries"] = data.Metadata.Retries;
                telemetry["metadata.cancelled"] = data.Metadata.Cancelled;
                telemetry["metadata.failed"] = data.Metadata.Failed;
                telemetry["metadata.bytes.total"] = data.Metadata.TotalBytes;
                telemetry["metadata.bytes.max"] = data.Metadata.MaxBytes;

                if (data.Metadata.StatusCodes.Count > 0)
                {
                    telemetry.AddComplexData("metadata.http.statuscodes", ToStatusCodeTelemetry(data.Metadata.StatusCodes));
                }

                if (data.Metadata.EventTiming.Requests > 0)
                {
                    telemetry["metadata.timing.min"] = data.Metadata.EventTiming.MinDuration.TotalMilliseconds;
                    telemetry["metadata.timing.mean"] = data.Metadata.EventTiming.TotalDuration.TotalMilliseconds / data.Metadata.EventTiming.Requests;
                    telemetry["metadata.timing.max"] = data.Metadata.EventTiming.MaxDuration.TotalMilliseconds;
                }

                if (data.Metadata.HeaderTiming != null && data.Metadata.HeaderTiming.Requests > 0)
                {
                    telemetry["metadata.header.timing.min"] = data.Metadata.HeaderTiming.MinDuration.TotalMilliseconds;
                    telemetry["metadata.header.timing.mean"] = data.Metadata.HeaderTiming.TotalDuration.TotalMilliseconds / data.Metadata.HeaderTiming.Requests;
                    telemetry["metadata.header.timing.max"] = data.Metadata.HeaderTiming.MaxDuration.TotalMilliseconds;
                }
            }

            // nupkgs
            lock (data.Nupkg.Lock)
            {
                telemetry["nupkg.requests"] = data.Nupkg.EventTiming.Requests;
                telemetry["nupkg.success"] = data.Nupkg.Successful;
                telemetry["nupkg.retries"] = data.Nupkg.Retries;
                telemetry["nupkg.cancelled"] = data.Nupkg.Cancelled;
                telemetry["nupkg.failed"] = data.Nupkg.Failed;
                telemetry["nupkg.bytes.total"] = data.Nupkg.TotalBytes;
                telemetry["nupkg.bytes.max"] = data.Nupkg.MaxBytes;

                if (data.Nupkg.StatusCodes.Count > 0)
                {
                    telemetry.AddComplexData("nupkg.http.statuscodes", ToStatusCodeTelemetry(data.Nupkg.StatusCodes));
                }

                if (data.Nupkg.EventTiming.Requests > 0)
                {
                    telemetry["nupkg.timing.min"] = data.Nupkg.EventTiming.MinDuration.TotalMilliseconds;
                    telemetry["nupkg.timing.mean"] = data.Nupkg.EventTiming.TotalDuration.TotalMilliseconds / data.Nupkg.EventTiming.Requests;
                    telemetry["nupkg.timing.max"] = data.Nupkg.EventTiming.MaxDuration.TotalMilliseconds;
                }

                if (data.Nupkg.HeaderTiming != null && data.Nupkg.HeaderTiming.Requests > 0)
                {
                    telemetry["nupkg.header.timing.min"] = data.Nupkg.HeaderTiming.MinDuration.TotalMilliseconds;
                    telemetry["nupkg.header.timing.mean"] = data.Nupkg.HeaderTiming.TotalDuration.TotalMilliseconds / data.Nupkg.HeaderTiming.Requests;
                    telemetry["nupkg.header.timing.max"] = data.Nupkg.HeaderTiming.MaxDuration.TotalMilliseconds;
                }
            }

            return telemetry;
        }

        private static TelemetryEvent ToStatusCodeTelemetry(Dictionary<int, int> statusCodes)
        {
            var subevent = new TelemetryEvent(null);

            foreach (var pair in statusCodes)
            {
                subevent[pair.Key.ToString()] = pair.Value;
            }

            return subevent;
        }

        private static string GetMsFeed(PackageSource source)
        {
            if (source.IsHttp)
            {
                if (TelemetryUtility.IsNuGetOrg(source))
                {
                    return "nuget.org";
                }
                else if (TelemetryUtility.IsAzureArtifacts(source))
                {
                    return "Azure Artifacts";
                }
                else if (TelemetryUtility.IsGitHub(source))
                {
                    return "GitHub Package Registry";
                }
            }
            else if (source.IsLocal)
            {
                if (TelemetryUtility.IsVsOfflineFeed(source))
                {
                    return "VS Offline";
                }
            }

            return null;
        }

        public class Data
        {
            public ResourceData Metadata { get; }
            public ResourceData Nupkg { get; }

            public Data()
            {
                Metadata = new ResourceData();
                Nupkg = new ResourceData();
            }
        }

        public class ResourceData
        {
            public object Lock = new object();
            public ResourceTimingData EventTiming = new ResourceTimingData();
            public ResourceTimingData HeaderTiming;
            public long TotalBytes;
            public long MaxBytes;
            public Dictionary<int, int> StatusCodes = new Dictionary<int, int>();
            public int Successful;
            public int Retries;
            public int Cancelled;
            public int Failed;
        }

        public class ResourceTimingData
        {
            public int Requests;
            public TimeSpan TotalDuration;
            public TimeSpan MinDuration = TimeSpan.MaxValue;
            public TimeSpan MaxDuration;
        }
    }
}
