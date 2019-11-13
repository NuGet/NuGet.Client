// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Utility;

namespace NuGet.VisualStudio.Telemetry
{
    public sealed class PackageSourceTelemetry : IDisposable
    {
        private readonly ConcurrentDictionary<string, Data> _data;
        private readonly IDictionary<string, PackageSource> _sources;
        private readonly Guid _parentId;

        internal static readonly string EventName = "PackageSourceDiagnostics";

        public PackageSourceTelemetry(IEnumerable<PackageSource> sources, Guid parentId)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            _data = new ConcurrentDictionary<string, Data>();
            ProtocolDiagnostics.Event += ProtocolDiagnostics_Event;
            _sources = sources.ToDictionary(s => s.Source);
            _parentId = parentId;
        }

        private void ProtocolDiagnostics_Event(ProtocolDiagnosticEvent pdEvent)
        {
            AddAggregateData(pdEvent, _data);
        }

        internal static void AddAggregateData(ProtocolDiagnosticEvent pdEvent, ConcurrentDictionary<string, Data> allData)
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

                if (pdEvent.IsLastAttempt && !pdEvent.IsSuccess && !pdEvent.IsCancelled)
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
        }

        public void SendTelemetry()
        {
            var parentId = _parentId.ToString();
            foreach (var kvp in _data)
            {
                Data data = kvp.Value;
                string source = kvp.Key;
                if (!_sources.TryGetValue(kvp.Key, out PackageSource packageSource))
                {
                    packageSource = null;
                }

                var telemetry = ToTelemetry(data, source, packageSource, parentId);

                if (telemetry != null)
                {
                    TelemetryActivity.EmitTelemetryEvent(telemetry);
                }
            }
        }

        internal static TelemetryEvent ToTelemetry(Data data, string source, PackageSource packageSource, string parentId)
        {
            if (data.Metadata.EventTiming.Requests == 0 && data.Nupkg.EventTiming.Requests == 0)
            {
                return null;
            }

            var telemetry = new TelemetryEvent(EventName,
                new Dictionary<string, object>()
                {
                    { PropertyNames.ParentId, parentId },
                });

            // source info
            telemetry.AddPiiData(PropertyNames.Source.Url, source);

            if (packageSource == null)
            {
                packageSource = new PackageSource(source);
            }

            if (packageSource.IsHttp)
            {
                telemetry[PropertyNames.Source.Type] = "http";
                telemetry[PropertyNames.Source.Protocol] = TelemetryUtility.IsHttpV3(packageSource) ? 3 : packageSource.ProtocolVersion;
            }
            else
            {
                telemetry[PropertyNames.Source.Type] = "local";
                telemetry[PropertyNames.Source.Protocol] = packageSource.ProtocolVersion;
            }

            var msFeed = GetMsFeed(packageSource);
            if (msFeed != null)
            {
                telemetry[PropertyNames.Source.MSFeed] = msFeed;
            }

            // metadata
            lock (data.Metadata.Lock)
            {
                telemetry[PropertyNames.Metadata.Requests] = data.Metadata.EventTiming.Requests;
                telemetry[PropertyNames.Metadata.Successful] = data.Metadata.Successful;
                telemetry[PropertyNames.Metadata.Retries] = data.Metadata.Retries;
                telemetry[PropertyNames.Metadata.Cancelled] = data.Metadata.Cancelled;
                telemetry[PropertyNames.Metadata.Failed] = data.Metadata.Failed;
                telemetry[PropertyNames.Metadata.Bytes.Total] = data.Metadata.TotalBytes;
                telemetry[PropertyNames.Metadata.Bytes.Max] = data.Metadata.MaxBytes;

                if (data.Metadata.StatusCodes.Count > 0)
                {
                    telemetry.ComplexData[PropertyNames.Metadata.Http.StatusCodes] = ToStatusCodeTelemetry(data.Metadata.StatusCodes);
                }

                if (data.Metadata.EventTiming.Requests > 0)
                {
                    telemetry[PropertyNames.Metadata.Timing.Min] = data.Metadata.EventTiming.MinDuration.TotalMilliseconds;
                    telemetry[PropertyNames.Metadata.Timing.Mean] = data.Metadata.EventTiming.TotalDuration.TotalMilliseconds / data.Metadata.EventTiming.Requests;
                    telemetry[PropertyNames.Metadata.Timing.Max] = data.Metadata.EventTiming.MaxDuration.TotalMilliseconds;
                }

                if (data.Metadata.HeaderTiming != null && data.Metadata.HeaderTiming.Requests > 0)
                {
                    telemetry[PropertyNames.Metadata.Header.Timing.Min] = data.Metadata.HeaderTiming.MinDuration.TotalMilliseconds;
                    telemetry[PropertyNames.Metadata.Header.Timing.Mean] = data.Metadata.HeaderTiming.TotalDuration.TotalMilliseconds / data.Metadata.HeaderTiming.Requests;
                    telemetry[PropertyNames.Metadata.Header.Timing.Max] = data.Metadata.HeaderTiming.MaxDuration.TotalMilliseconds;
                }
            }

            // nupkgs
            lock (data.Nupkg.Lock)
            {
                telemetry[PropertyNames.Nupkg.Requests] = data.Nupkg.EventTiming.Requests;
                telemetry[PropertyNames.Nupkg.Successful] = data.Nupkg.Successful;
                telemetry[PropertyNames.Nupkg.Retries] = data.Nupkg.Retries;
                telemetry[PropertyNames.Nupkg.Cancelled] = data.Nupkg.Cancelled;
                telemetry[PropertyNames.Nupkg.Failed] = data.Nupkg.Failed;
                telemetry[PropertyNames.Nupkg.Bytes.Total] = data.Nupkg.TotalBytes;
                telemetry[PropertyNames.Nupkg.Bytes.Max] = data.Nupkg.MaxBytes;

                if (data.Nupkg.StatusCodes.Count > 0)
                {
                    telemetry.ComplexData[PropertyNames.Nupkg.Http.StatusCodes] = ToStatusCodeTelemetry(data.Nupkg.StatusCodes);
                }

                if (data.Nupkg.EventTiming.Requests > 0)
                {
                    telemetry[PropertyNames.Nupkg.Timing.Min] = data.Nupkg.EventTiming.MinDuration.TotalMilliseconds;
                    telemetry[PropertyNames.Nupkg.Timing.Mean] = data.Nupkg.EventTiming.TotalDuration.TotalMilliseconds / data.Nupkg.EventTiming.Requests;
                    telemetry[PropertyNames.Nupkg.Timing.Max] = data.Nupkg.EventTiming.MaxDuration.TotalMilliseconds;
                }

                if (data.Nupkg.HeaderTiming != null && data.Nupkg.HeaderTiming.Requests > 0)
                {
                    telemetry[PropertyNames.Nupkg.Header.Timing.Min] = data.Nupkg.HeaderTiming.MinDuration.TotalMilliseconds;
                    telemetry[PropertyNames.Nupkg.Header.Timing.Mean] = data.Nupkg.HeaderTiming.TotalDuration.TotalMilliseconds / data.Nupkg.HeaderTiming.Requests;
                    telemetry[PropertyNames.Nupkg.Header.Timing.Max] = data.Nupkg.HeaderTiming.MaxDuration.TotalMilliseconds;
                }
            }

            return telemetry;
        }

        private static TelemetryEvent ToStatusCodeTelemetry(Dictionary<int, int> statusCodes)
        {
            var subevent = new TelemetryEvent(eventName: null);

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
                    return "Azure DevOps";
                }
                else if (TelemetryUtility.IsGitHub(source))
                {
                    return "GitHub";
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

        internal class Data
        {
            internal ResourceData Metadata { get; }
            internal ResourceData Nupkg { get; }

            internal Data()
            {
                Metadata = new ResourceData();
                Nupkg = new ResourceData();
            }
        }

        internal class ResourceData
        {
            public readonly object Lock = new object();
            public readonly ResourceTimingData EventTiming = new ResourceTimingData();
            public ResourceTimingData HeaderTiming;
            public long TotalBytes;
            public long MaxBytes;
            public readonly Dictionary<int, int> StatusCodes = new Dictionary<int, int>();
            public int Successful;
            public int Retries;
            public int Cancelled;
            public int Failed;
        }

        internal class ResourceTimingData
        {
            public int Requests;
            public TimeSpan TotalDuration;
            public TimeSpan MinDuration = TimeSpan.MaxValue;
            public TimeSpan MaxDuration;
        }

        internal static class PropertyNames
        {
            internal static readonly string ParentId = "parentid";

            internal static class Source
            {
                internal static readonly string Url = "source.url";
                internal static readonly string Type = "source.type";
                internal static readonly string Protocol = "source.protocol";
                internal static readonly string MSFeed = "source.msfeed";
            }

            internal static class Metadata
            {
                internal static readonly string Requests = "metadata.requests";
                internal static readonly string Successful = "metadata.successful";
                internal static readonly string Retries = "metadata.retries";
                internal static readonly string Cancelled = "metadata.cancelled";
                internal static readonly string Failed = "metadata.failed";

                internal static class Bytes
                {
                    internal static readonly string Total = "metadata.bytes.total";
                    internal static readonly string Max = "metadata.bytes.max";
                }

                internal static class Http
                {
                    internal static readonly string StatusCodes = "metadata.http.statuscodes";
                }

                internal static class Timing
                {
                    internal static readonly string Min = "metadata.timing.min";
                    internal static readonly string Mean = "metadata.timing.mean";
                    internal static readonly string Max = "metadata.timing.max";
                }

                internal static class Header
                {
                    internal static class Timing
                    {
                        internal static readonly string Min = "metadata.header.timing.min";
                        internal static readonly string Mean = "metadata.header.timing.mean";
                        internal static readonly string Max = "metadata.header.timing.max";
                    }
                }
            }

            internal static class Nupkg
            {
                internal static readonly string Requests = "nupkg.requests";
                internal static readonly string Successful = "nupkg.successful";
                internal static readonly string Retries = "nupkg.retries";
                internal static readonly string Cancelled = "nupkg.cancelled";
                internal static readonly string Failed = "nupkg.failed";

                internal static class Bytes
                {
                    internal static readonly string Total = "nupkg.bytes.total";
                    internal static readonly string Max = "nupkg.bytes.max";
                }

                internal static class Http
                {
                    internal static readonly string StatusCodes = "nupkg.http.statuscodes";
                }

                internal static class Timing
                {
                    internal static readonly string Min = "nupkg.timing.min";
                    internal static readonly string Mean = "nupkg.timing.mean";
                    internal static readonly string Max = "nupkg.timing.max";
                }

                internal static class Header
                {
                    internal static class Timing
                    {
                        internal static readonly string Min = "nupkg.header.timing.min";
                        internal static readonly string Mean = "nupkg.header.timing.mean";
                        internal static readonly string Max = "nupkg.header.timing.max";
                    }
                }
            }
        }
    }
}
