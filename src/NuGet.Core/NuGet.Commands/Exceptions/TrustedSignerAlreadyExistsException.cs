// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Commands.Exceptions
{
    [Serializable]
    public class TrustedSignerAlreadyExistsException : InvalidOperationException
    {
        public TrustedSignerAlreadyExistsException() : base()
        {
        }

        public TrustedSignerAlreadyExistsException(string message)
            : base(message)
        {
        }
    }
}
