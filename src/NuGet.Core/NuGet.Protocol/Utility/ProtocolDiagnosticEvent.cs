// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Utility
{
    public class ProtocolDiagnosticEvent
    {
        public DateTime Timestamp { get; }
        public string Source { get; }
        public string Url { get; }
        public TimeSpan? HeaderDuration { get; }
        public TimeSpan EventDuration { get; }
        public int? HttpStatusCode { get; }
        public long Bytes { get; }
        public bool IsSuccess { get; }
        public bool IsRetry { get; }
        public bool IsCancelled { get; }

        internal ProtocolDiagnosticEvent(
            DateTime timestamp,
            string source,
            string url,
            TimeSpan? headerDuration,
            TimeSpan eventDuration,
            int? httpStatusCode,
            long bytes,
            bool isSuccess,
            bool isRetry,
            bool isCancelled)
        {
            Timestamp = timestamp;
            Source = source;
            Url = url;
            HeaderDuration = headerDuration;
            EventDuration = eventDuration;
            HttpStatusCode = httpStatusCode;
            Bytes = bytes;
            IsSuccess = isSuccess;
            IsRetry = isRetry;
            IsCancelled = isCancelled;
        }
    }
}
