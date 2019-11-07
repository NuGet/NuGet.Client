// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Utility
{
    public abstract class ProtocolDiagnosticEventBase
    {
        public Uri Source { get; }
        public Uri Url { get; }
        public TimeSpan? HeaderDuration { get; }
        public int? HttpStatusCode { get; }
        public bool IsRetry { get; }
        public bool IsCancelled { get; }
        public bool IsLastAttempt { get; }

        protected ProtocolDiagnosticEventBase(ProtocolDiagnosticEventBase other)
            : this(other.Source,
                  other.Url,
                  other.HeaderDuration,
                  other.HttpStatusCode,
                  other.IsRetry,
                  other.IsCancelled,
                  other.IsLastAttempt)
        {
        }

        protected ProtocolDiagnosticEventBase(
            Uri source,
            Uri url,
            TimeSpan? headerDuration,
            int? httpStatusCode,
            bool isRetry,
            bool isCancelled,
            bool isLastAttempt)
        {
            Source = source;
            Url = url;
            HeaderDuration = headerDuration;
            HttpStatusCode = httpStatusCode;
            IsRetry = isRetry;
            IsCancelled = isCancelled;
            IsLastAttempt = isLastAttempt;
        }
    }
}
