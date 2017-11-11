// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Exceptions that are generated while creating a package timestamp.
    /// </summary>
    internal class TimestampException : Exception, ILogMessageException
    {
        private readonly ILogMessage _logMessage;

        public TimestampException(ILogMessage logMessage)
            : base(logMessage?.Message)
        {
            _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
        }

        public ILogMessage AsLogMessage()
        {
            return _logMessage;
        }
    }
}
