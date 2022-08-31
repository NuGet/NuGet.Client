// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio.Implementation.Exceptions
{
    internal class AssetsFileMissingException : InvalidOperationException
    {
        public AssetsFileMissingException() : base()
        {
        }

        public AssetsFileMissingException(string message) : base(message)
        {
        }

        public AssetsFileMissingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
