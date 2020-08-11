// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Protocol.Core.Types
{
    public class FatalProtocolException : NuGetProtocolException
    {
        public FatalProtocolException(string message) : base(message)
        {
        }

        public FatalProtocolException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public FatalProtocolException(string message, NuGetLogCode logCode) : base(message)
        {
            LogCode = logCode;
        }

        public FatalProtocolException(string message, Exception innerException, NuGetLogCode logCode) : base(message, innerException)
        {
            LogCode = logCode;
        }

        public NuGetLogCode LogCode { get; } = NuGetLogCode.NU1300;
    }
}
