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
            ProtocolDiagnostics.ResourceEvent += ProtocolDiagnostics_ResourceEvent;
            _parentId = parentId;

            // Multiple sources can use the same feed url. We can't know which one protocol events come from, so choose any.
            _sources = new Dictionary<string, PackageSource>();
            foreach (var source in sources)
            {
                _sources[source.Source] = source;
            }
        }

        private void ProtocolDiagnostics_ResourceEvent(ProtocolDiagnosticResourceEvent pdrEvent)
        {
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
        }

        public void Dispose()
        {
            ProtocolDiagnostics.Event -= ProtocolDiagnostics_Event;
            ProtocolDiagnostics.ResourceEvent -= ProtocolDiagnostics_ResourceEvent;
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

            AddResourceProperties(telemetry, data.Metadata, PropertyNames.Metadata);
            AddResourceProperties(telemetry, data.Nupkg, PropertyNames.Nupkg);

            ResourceData all = CalculateTotal(data.Metadata, data.Nupkg);
            AddResourceProperties(telemetry, all, PropertyNames.All);

            return telemetry;
        }

        private static void AddResourceProperties(TelemetryEvent telemetry, ResourceData data, PackageSourceDiagnosticsPropertyNames.ResourcePropertyNames propertyNames)
        {
            lock (data.Lock)
            {
                telemetry[propertyNames.Requests] = data.EventTiming.Requests;
                telemetry[propertyNames.Successful] = data.Successful;
                telemetry[propertyNames.Retries] = data.Retries;
                telemetry[propertyNames.Cancelled] = data.Cancelled;
                telemetry[propertyNames.Failed] = data.Failed;
                telemetry[propertyNames.Bytes.Total] = data.TotalBytes;
                telemetry[propertyNames.Bytes.Max] = data.MaxBytes;

                if (data.StatusCodes.Count > 0)
                {
                    telemetry.ComplexData[propertyNames.Http.StatusCodes] = ToStatusCodeTelemetry(data.StatusCodes);
                }

                if (data.EventTiming.Requests > 0)
                {
                    telemetry[propertyNames.Timing.Total] = data.EventTiming.TotalDuration.TotalMilliseconds;
                }

                if (data.HeaderTiming != null && data.HeaderTiming.Requests > 0)
                {
                    telemetry[propertyNames.Header.Timing.Total] = data.HeaderTiming.TotalDuration.TotalMilliseconds;
                }
            }
        }

        private static ResourceData CalculateTotal(params ResourceData[] data)
        {
            var all = new ResourceData();

            foreach (ResourceData resourceData in data)
            {
                lock (resourceData.Lock)
                {
                    all.EventTiming.Requests += resourceData.EventTiming.Requests;
                    all.EventTiming.TotalDuration += resourceData.EventTiming.TotalDuration;
                    if (resourceData.HeaderTiming != null)
                    {
                        if (all.HeaderTiming == null)
                        {
                            all.HeaderTiming = new ResourceTimingData();
                        }

                        all.HeaderTiming.Requests += resourceData.HeaderTiming.Requests;
                        all.HeaderTiming.TotalDuration += resourceData.HeaderTiming.TotalDuration;
                    }

                    all.TotalBytes += resourceData.TotalBytes;
                    all.MaxBytes += resourceData.MaxBytes;

                    foreach (var kvp in resourceData.StatusCodes)
                    {
                        if (!all.StatusCodes.TryGetValue(kvp.Key, out int count))
                        {
                            count = 0;
                        }
                        count += kvp.Value;
                        all.StatusCodes[kvp.Key] = count;
                    }

                    all.Successful += resourceData.Successful;
                    all.Retries += resourceData.Retries;
                    all.Cancelled += resourceData.Cancelled;
                    all.Failed += resourceData.Failed;
                }
            }

            return all;
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

        public Totals GetTotals()
        {
            return GetTotals(_data);
        }

        internal static Totals GetTotals(ConcurrentDictionary<string, Data> data)
        {
            int requests = 0;
            long bytes = 0;
            TimeSpan duration = TimeSpan.Zero;

            foreach (var source in data)
            {
                Increment(source.Value.Metadata, ref requests, ref bytes, ref duration);
                Increment(source.Value.Nupkg, ref requests, ref bytes, ref duration);
            }

            return new Totals(requests, bytes, duration);

            void Increment(ResourceData rd, ref int r, ref long b, ref TimeSpan d)
            {
                lock (rd.Lock)
                {
                    r += rd.EventTiming.Requests;
                    b += rd.TotalBytes;
                    d += rd.EventTiming.TotalDuration;
                }
            }
        }

        public class Totals
        {
            public Totals(int requests, long bytes, TimeSpan duration)
            {
                Requests = requests;
                Bytes = bytes;
                Duration = duration;
            }

            public int Requests { get; }
            public long Bytes { get; }
            public TimeSpan Duration { get; }
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
        }

        internal static PackageSourceDiagnosticsPropertyNames PropertyNames = new PackageSourceDiagnosticsPropertyNames();

        internal class PackageSourceDiagnosticsPropertyNames
        {
            internal string ParentId { get; } = "parentid";
            internal SourcePropertyNames Source { get; } = new SourcePropertyNames("source");
            internal ResourcePropertyNames Metadata { get; } = new ResourcePropertyNames("metadata");
            internal ResourcePropertyNames Nupkg { get; } = new ResourcePropertyNames("nupkg");
            internal ResourcePropertyNames All { get; } = new ResourcePropertyNames("all");

            internal class ResourcePropertyNames
            {
                internal ResourcePropertyNames(string prefix)
                {
                    Requests = prefix + ".requests";
                    Successful = prefix + ".successful";
                    Retries = prefix + ".retries";
                    Cancelled = prefix + ".cancelled";
                    Failed = prefix + ".failed";
                    Bytes = new BytesPropertyNames(prefix + ".bytes");
                    Http = new HttpPropertyNames(prefix + ".http");
                    Timing = new TimingPropertyNames(prefix + ".timing");
                    Header = new HeaderPropertyNames(prefix + ".header");
                }

                internal string Requests { get; }
                internal string Successful { get; }
                internal string Retries { get; }
                internal string Cancelled { get; }
                internal string Failed { get; }
                internal BytesPropertyNames Bytes { get; }
                internal HttpPropertyNames Http { get; }
                internal TimingPropertyNames Timing { get; }
                internal HeaderPropertyNames Header { get; }
            }

            internal class SourcePropertyNames
            {
                public SourcePropertyNames(string prefix)
                {
                    Url = prefix + ".url";
                    Type = prefix + ".type";
                    Protocol = prefix + ".nugetprotocol";
                    MSFeed = prefix + ".msfeed";
                }

                internal string Url { get; }
                internal string Type { get; }
                internal string Protocol { get; }
                internal string MSFeed { get; }
            }

            internal class TimingPropertyNames
            {
                public TimingPropertyNames(string prefix)
                {
                    Total = prefix + ".total";
                }

                internal string Total { get; }
            }

            internal class HeaderPropertyNames
            {
                internal HeaderPropertyNames(string prefix)
                {
                    Timing = new TimingPropertyNames(prefix + ".timing");
                }

                internal TimingPropertyNames Timing { get; }
            }

            internal class HttpPropertyNames
            {
                internal HttpPropertyNames(string prefix)
                {
                    StatusCodes = prefix + ".statuscodes";
                }

                internal string StatusCodes { get; }
            }

            internal class BytesPropertyNames
            {
                internal  BytesPropertyNames(string prefix)
                {
                    Total = prefix + ".total";
                    Max = prefix + ".max";
                }
                internal string Total { get; }
                internal string Max { get; }
            }
        }
    }
}
