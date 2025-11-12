// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Runtime.Serialization;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Exception thrown when a quota limit is exceeded (HTTP 403 Forbidden with Retry-After header).
    /// </summary>
    [Serializable]
    public class QuotaExceededException : FatalProtocolException
    {
        /// <summary>
        /// Gets the time to wait before retrying, if specified in the Retry-After header.
        /// </summary>
        public TimeSpan? RetryAfter { get; }

        public QuotaExceededException(string message) : base(message)
        {
        }

        public QuotaExceededException(string message, TimeSpan? retryAfter) : base(message)
        {
            RetryAfter = retryAfter;
        }

        public QuotaExceededException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public QuotaExceededException(string message, TimeSpan? retryAfter, Exception innerException) : base(message, innerException)
        {
            RetryAfter = retryAfter;
        }

#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")] // https://github.com/dotnet/docs/issues/34893
#endif
        protected QuotaExceededException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            RetryAfter = (TimeSpan?)info.GetValue(nameof(RetryAfter), typeof(TimeSpan?));
        }

#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")] // https://github.com/dotnet/docs/issues/34893
#endif
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(RetryAfter), RetryAfter);
        }
    }
}
