// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Utility
{
    internal sealed class ProtocolDiagnosticInProgressEvent : ProtocolDiagnosticEventBase
    {
        internal ProtocolDiagnosticInProgressEvent(
            string source,
            string url,
            TimeSpan? headerDuration,
            int? httpStatusCode,
            bool isRetry,
            bool isCancelled,
            bool isLastAttempt)
            : base(source, url, headerDuration, httpStatusCode, isRetry, isCancelled, isLastAttempt)
        {
        }
    }
}
