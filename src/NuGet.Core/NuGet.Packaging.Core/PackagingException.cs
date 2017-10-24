﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Packaging.Core
{
    public class PackagingException : Exception, ILogMessageException
    {
        private readonly IPackLogMessage _logMessage;

        public PackagingException(string message)
            : base(message)
        {
            _logMessage = PackLogMessage.CreateError(NuGetLogCode.NU5000, message);
        }

        public PackagingException(NuGetLogCode logCode, string message)
            : base(message)
        {
            _logMessage = PackLogMessage.CreateError(logCode, message);
        }

        public PackagingException(string message, Exception innerException)
            : base(message, innerException)
        {
            _logMessage = PackLogMessage.CreateError(NuGetLogCode.NU5000, message);
        }

        public virtual ILogMessage AsLogMessage()
        {
            return _logMessage;
        }
    }
}
