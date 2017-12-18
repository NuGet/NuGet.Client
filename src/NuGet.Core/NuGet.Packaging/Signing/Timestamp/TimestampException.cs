// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Exceptions that are generated while creating a package timestamp.
    /// </summary>
    public class TimestampException : SignatureException
    {
        private readonly ILogMessage _logMessage;

        public TimestampException()
            : base(string.Empty)
        {
        }

        public TimestampException(NuGetLogCode code, string message)
            : base(code, message)
        {
        }

        public TimestampException(string message)
            : this(NuGetLogCode.NU3000, message)
        {
        }

        public TimestampException(ILogMessage logMessage)
            : base(logMessage?.Message)
        {
            _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
        }

        public override ILogMessage AsLogMessage()
        {
            return _logMessage;
        }
    }
}
