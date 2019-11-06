// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Utility
{
    public sealed class ProtocolDiagnosticEvent : ProtocolDiagnosticEventBase
    {
        public DateTime Timestamp { get; }
        public TimeSpan EventDuration { get; }
        public long Bytes { get; }
        public bool IsSuccess { get; }

        internal ProtocolDiagnosticEvent(
            DateTime timestamp,
            string source,
            string url,
            TimeSpan? headerDuration,
            TimeSpan eventDuration,
            long bytes,
            int? httpStatusCode,
            bool isSuccess,
            bool isRetry,
            bool isCancelled,
            bool isLastAttempt)
            : this(
                  timestamp,
                  eventDuration,
                  bytes,
                  isSuccess,
                  new ProtocolDiagnosticInProgressEvent(
                      source,
                      url,
                      headerDuration,
                       httpStatusCode,
                       isRetry,
                       isCancelled,
                       isLastAttempt))
        {
        }

        internal ProtocolDiagnosticEvent(
            DateTime timestamp,
            TimeSpan eventDuration,
            long bytes,
            bool isSuccess,
            ProtocolDiagnosticEventBase eventBase)
            : base(eventBase)
        {
            Timestamp = timestamp;
            EventDuration = eventDuration;
            Bytes = bytes;
            IsSuccess = isSuccess;
        }
    }
}
