﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Protocol.Core.Types
{
    public class FatalProtocolException : NuGetProtocolException
    {
        public FatalProtocolException(Exception innerException) :
            base(ExceptionUtilities.DisplayMessage(innerException), innerException)
        {
        }

        public FatalProtocolException(string message) : base(message)
        {
        }

        public FatalProtocolException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
